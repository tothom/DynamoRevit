using Dynamo.Wpf.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using RevitServices.Transactions;
using Dynamo.Wpf.Properties;
using Dynamo.Models;
using Dynamo.Extensions;

namespace Dynamo.ReadOnlyViewExtension
{
    public class ViewExtension: IViewExtension
    {
        private ICommandExecutive commandExecutive;

        public string UniqueId
        {
            get { return "0F2909FE-2EC0-446C-8462-0651A0C4E10D"; }
        }

        public static readonly string ExtensionName = "ReadOnlyUI";

        public string Name
        {
            get { return ExtensionName; }
        }

        public void Startup(ViewStartupParams viewStartupParams)
        {
            
        }

        public void Loaded(ViewLoadedParams viewLoadedParams)
        {
            commandExecutive = viewLoadedParams.CommandExecutive;

            // Adding a button in view menu to refresh and show manually
            var ReadOnlyMenuItem = new MenuItem { Header = "Read Only Mode", IsCheckable = true, IsChecked = false };
            ReadOnlyMenuItem.Click += (sender, args) =>
            {
                if (ReadOnlyMenuItem.IsChecked)
                {
                    TransactionManager.Instance.ReadOnlyMode = true;
                }
                else
                {
                    TransactionManager.Instance.ReadOnlyMode = false;
                    var cmd = new DynamoModel.ForceRunCancelCommand(true, false);
                    commandExecutive.ExecuteCommand(cmd, Guid.NewGuid().ToString(), ViewExtension.ExtensionName);
                }
            };
            viewLoadedParams.AddMenuItem(MenuBarType.View, ReadOnlyMenuItem);
        }

        public void Dispose()
        {
            
        }

        public void Shutdown()
        {

        }

    }
}
