using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Dynamo.Controls;
using Dynamo.Graph.Nodes;
using Dynamo.Graph.Workspaces;
using Dynamo.Models;
using Dynamo.ViewModels;
using Dynamo.Views;
using RevitServices.Transactions;

namespace Dynamo.Applications.ViewModel
{
    class ReadOnlyModeManager
    {
        private readonly DynamoModel model;
        private readonly DynamoViewModel viewModel;
        private readonly Window dynamoView;
        private readonly Dispatcher dispatcher;
        private WorkspaceModel currentWorkspace;
        private readonly WorkspaceViewModel currentWorkspaceViewModel;
        private static ObservableCollection<ReadOnlyNodeViewModel> viewModels;
        private static CollectionContainer viewModelCollection;

        public ReadOnlyModeManager(DynamoModel model, DynamoViewModel viewModel, Window dynamoView)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            this.dynamoView = dynamoView ?? throw new ArgumentNullException(nameof(dynamoView));
            this.dispatcher = dynamoView.Dispatcher;
            this.currentWorkspace = model.CurrentWorkspace;
            this.currentWorkspaceViewModel = viewModel.CurrentSpaceViewModel;
            TransactionManager.Instance.ReadOnlyModeChanged += OnReadOnlyModeChanged;

            SubscribeModelEvents();
            SubscribeWorkspaceEvents();
            AddDataTemplate();
            InitializeWorkspaceElement(viewModel.CurrentSpaceViewModel);
            InitializeGraphVisualization();
        }

        /// <summary>
        /// Initialize the ViewModels CompositeCollection and adds it to the 
        /// WorkspaceViewModels WorkspaceElements.
        /// Note: this should be done before the WorkspaceView and BaseVisualizationViewModel is initialized
        /// </summary>
        /// <param name="workspaceViewModel"></param>
        public static void InitializeWorkspaceElement(WorkspaceViewModel workspaceViewModel)
        {
            if (viewModels != null && viewModels.Count != 0)
            {
                viewModels.Clear();
            }

            if (IsWorkspaceElementInitialization(workspaceViewModel))
            {
                return;
            }

            viewModels = new ObservableCollection<ReadOnlyNodeViewModel>();
            viewModelCollection = new CollectionContainer { Collection = viewModels };
            workspaceViewModel.WorkspaceElements.Add(viewModelCollection);
        }

        #region Event Handlers

        private void OnReadOnlyModeChanged(object sender, EventArgs e)
        {
            if (!TransactionManager.Instance.ReadOnlyMode)
            {
                viewModels.Clear();
                UnSubscribeNodes();
                return;
            }

            InitializeGraphVisualization();
        }

        private void SubscribeModelEvents()
        {
            this.model.PropertyChanged += OnCurrentWorkspaceChanged;
        }


        private void SubscribeWorkspaceEvents()
        {
            if (currentWorkspace is null)
                return;

            currentWorkspace.NodeAdded += OnNodeAdded;
            currentWorkspace.NodeRemoved += OnNodeRemoved;
        }


        private void UnSubscribeWorkspaceEvents()
        {
            if (currentWorkspace is null)
                return;

            currentWorkspace.NodeAdded -= OnNodeAdded;
            currentWorkspace.NodeRemoved -= OnNodeRemoved;
        }

        private void SubscribeNodeEvents(NodeModel node)
        {
            if (node is null)
                throw new ArgumentNullException(nameof(node));

            node.PropertyChanged += OnNodePropertyChanged;
        }

        private void UnSubscribeNodeEvents(NodeModel node)
        {
            if (node is null)
                throw new ArgumentNullException(nameof(node));

            node.PropertyChanged -= OnNodePropertyChanged;
        }

        private void UnSubscribeNodes()
        {
            foreach (var node in currentWorkspace.Nodes)
            {
                UnSubscribeNodeEvents(node);
            }
        }

        private void OnNodePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NodeModel.State))
            {
                var node = sender as NodeModel;
                if (node.State == ElementState.Warning)
                {
                    if (node.ToolTipText.Contains("Dynamo For Revit is in read - only mode"))
                    {
                        CreateReadOnlyViewMode(node);
                        return;
                    }

                    node.PropertyChanged += OnToolTipChanged;
                    return;
                }

                viewModels.Remove(viewModels.Where(x => x.GUID == node.GUID.ToString()).FirstOrDefault());
            }
        }

        private void OnToolTipChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NodeModel.ToolTipText))
            {
                var node = sender as NodeModel;
                CreateReadOnlyViewMode(node);
                node.PropertyChanged -= OnToolTipChanged;
            }

        }

        private void OnNodeAdded(NodeModel node)
        {
            SubscribeNodeEvents(node);   
        }
        private void OnNodeRemoved(NodeModel obj)
        {
            viewModels.Remove(viewModels.Where(x => x.GUID == obj.GUID.ToString()).FirstOrDefault());
            UnSubscribeNodeEvents(obj);
        }

        private void OnCurrentWorkspaceChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DynamoModel.CurrentWorkspace) &&
                !Equals(currentWorkspace, model.CurrentWorkspace))
            {
                this.UnSubscribeWorkspaceEvents();
                currentWorkspace = model.CurrentWorkspace;
                this.AddDataTemplate();
                InitializeWorkspaceElement(viewModel.CurrentSpaceViewModel);
                this.SubscribeWorkspaceEvents();
                this.InitializeGraphVisualization();
            }
        }

        #endregion

        #region Private helpers
        private static bool IsWorkspaceElementInitialization(WorkspaceViewModel workspaceViewModel)
        {
            return workspaceViewModel.WorkspaceElements.IndexOf(viewModelCollection) != -1;
        }

        private void AddDataTemplate()
        {
            var views = FindVisualChildren<WorkspaceView>(dynamoView);

            if (views.Any())
            {
                AddTempalteToResourceDictionary(views);
            }
            else
            {
                dynamoView.LayoutUpdated += OnDynamoViewUpdated;
            }
        }

        private void OnDynamoViewUpdated(object sender, EventArgs e)
        {
            var views = FindVisualChildren<WorkspaceView>(dynamoView);
            AddTempalteToResourceDictionary(views);
            dynamoView.LayoutUpdated -= this.OnDynamoViewUpdated;
        }

        private void AddTempalteToResourceDictionary(IEnumerable<WorkspaceView> workspaceView)
        {
            // Location ReadOnlyNodesVisualsTemplate.xaml file so the resource can be injected into the WorkspaceView
            var path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var uri = new Uri(System.IO.Path.Combine(path, @"Views\ReadOnlyNodesVisualsTemplate.xaml"));
            var visualizationDataTemplateDictionary = new ResourceDictionary { Source = uri };

            foreach (var view in workspaceView)
            {
                view.Resources.MergedDictionaries.Add(visualizationDataTemplateDictionary);
            }
        }

        private void InitializeGraphVisualization()
        {
            if (!TransactionManager.Instance.ReadOnlyMode)
                return;

            foreach (NodeModel node in currentWorkspace.Nodes)
            {
                OnNodeAdded(node);
            }
        }

        private void CreateReadOnlyViewMode(NodeModel value)
        {
            var nodeViewModel = NodeViewModelFromNodeModel(value);

            if (!value.ToolTipText.Contains("Dynamo For Revit is in read-only mode"))
                return;

            var viewModel = new ReadOnlyNodeViewModel(nodeViewModel);

            if (this.dispatcher.CheckAccess())
            {
                viewModels.Add(viewModel);
            }
            else
            {
                this.dispatcher.Invoke(
                    new Action(() => viewModels.Add(viewModel)), 
                    DispatcherPriority.Normal
                    );
            }
            
        }

        private NodeViewModel NodeViewModelFromNodeModel(NodeModel obj)
        {
            var nodeViewModels = viewModel.CurrentSpaceViewModel.Nodes;
            return nodeViewModels.Where(x => obj.GUID == x.NodeModel.GUID).FirstOrDefault();
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        #endregion
    }
}
