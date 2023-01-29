using CoreNodeModelsWpf.Nodes;
using DSRevitNodesUI;
using System;

namespace Dynamo.Wpf.Nodes.Revit
{
    [Obsolete("This class will be removed, please use the class in RevitNodesWPF")]
    public class FamilyInstanceParametersNodeViewCustomization : DropDownNodeViewCustomization, INodeViewCustomization<FamilyInstanceParameters>
    {
        [Obsolete("This method will be removed, please use the method in RevitNodesWPF")]
        public void CustomizeView(FamilyInstanceParameters model, Dynamo.Controls.NodeView nodeView)
        {
            base.CustomizeView(model, nodeView);
            
            // this is not a recommended workaround
            model.EngineController = nodeView.ViewModel.DynamoViewModel.EngineController;
        }
    }
}
