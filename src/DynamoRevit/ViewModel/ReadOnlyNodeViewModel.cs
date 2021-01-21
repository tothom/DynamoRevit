using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dynamo.Core;
using Dynamo.ViewModels;

namespace Dynamo.Applications.ViewModel
{
    public class ReadOnlyNodeViewModel : NotificationObject
    {
        private readonly NodeViewModel nodeViewModel;

        public bool Frozen
        {
            get { return nodeViewModel.IsFrozen; }
        }

        public double Width
        {
            get { return nodeViewModel.NodeLogic.Width; }
        }

        public double Height
        {
            get { return nodeViewModel.NodeLogic.Height; }
        }

        public double Top
        {
            get { return nodeViewModel.Top + 5; }
        }

        public double Left
        {
            get { return nodeViewModel.Left; }
        }

        private bool hideVisual;
        public bool HideVisual
        {
            get { return hideVisual; }
            set
            {
                if (hideVisual == value)
                    return;
                hideVisual = value;
                RaisePropertyChanged(nameof(HideVisual));
            }
        }

        // Groups have ZIndex of 1 and connectors have ZIndex 2.
        // To place the node colors under the connectors but above the connectors, we set it to 1.
        // This seems to work even though groups and node color views now have the same ZIndex.
        public double ZIndex { get { return nodeViewModel.ZIndex + 2; } } /*set { zIndex = value; RaisePropertyChanged(nameof(ZIndex)); } }*/

        public string GUID { get { return nodeViewModel.NodeLogic.GUID.ToString(); } }

        public ReadOnlyNodeViewModel(NodeViewModel nodeViewModel)
        {
            this.nodeViewModel = nodeViewModel;
            nodeViewModel.PropertyChanged += PropertyChangedHandler;
            nodeViewModel.NodeLogic.PropertyChanged += PropertyChangedHandler;
        }

        private void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Top":
                    RaisePropertyChanged(nameof(Top));
                    break;
                case "Left":
                    RaisePropertyChanged(nameof(Left));
                    break;
                case "IsFrozen":
                    RaisePropertyChanged(nameof(Frozen));
                    break;
                case "ZIndex":
                    RaisePropertyChanged(nameof(ZIndex));
                    break;
                case "Position":
                    RaisePropertyChanged(nameof(Width));
                    RaisePropertyChanged(nameof(Height));
                    break;
                default:
                    //no other cases to support
                    break;
            }
        }
    }
}
