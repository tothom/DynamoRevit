using System;
using Autodesk.Revit.UI.Events;

namespace RevitServicesUI.EventHandler
{
    /// <summary>
    /// This is a event handler proxy class to serve as a proxy between the event publisher and
    /// the event subscriber
    /// </summary>
    public class UIEventHandlerProxy
    {
        public event EventHandler<ViewActivatingEventArgs> ViewActivating;
        public event EventHandler<ViewActivatedEventArgs> ViewActivated;

        public void OnApplicationViewActivating(object sender, ViewActivatingEventArgs args)
        {
            InvokeEventHandler(ViewActivating, sender, args);
        }

        public void OnApplicationViewActivated(object sender, ViewActivatedEventArgs args)
        {
            InvokeEventHandler(ViewActivated, sender, args);
        }

        private void InvokeEventHandler<T>(EventHandler<T> eventHandler, object sender, T args) where T : EventArgs
        {
            var tempHandler = eventHandler;
            if (tempHandler != null)
            {
                tempHandler(sender, args);
            }
        }
    }
}
