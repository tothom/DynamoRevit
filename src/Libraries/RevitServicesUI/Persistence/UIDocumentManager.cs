using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitServices.Persistence;

namespace RevitServicesUI.Persistence
{

    /// <summary>
    /// Singleton class to manage Revit document resources
    /// </summary>
    public class UIDocumentManager
    {
        public static event Action<string> OnLogError;

        private UIDocument currentDocument;
        private UIApplication currentUIApplication;
        private static UIDocumentManager instance;
        private static DocumentManager dbInstance;
        private static readonly Object mutex = new Object();

        public static UIDocumentManager Instance
        {
            get
            {
                lock (mutex)
                {
                    return instance ?? (instance = new UIDocumentManager());
                }
            }
        }

        private UIDocumentManager()
        {
            dbInstance = DocumentManager.Instance;
        }

        /// <summary>
        /// Provides the currently active UI document.
        /// This is the document to which Dynamo is bound.
        /// </summary>
        public UIDocument CurrentUIDocument
        {
            get
            {
                return currentDocument;
            }
            set
            {
                currentDocument = value;
                dbInstance.CurrentDBDocument = currentDocument?.Document;
            }
        }

        /// <summary>
        /// Provides the current UIApplication
        /// </summary>
        public UIApplication CurrentUIApplication
        {
            get
            {
                return currentUIApplication;
            }
            set
            {
                currentUIApplication = value;
                dbInstance.CurrentApplication = currentUIApplication?.Application;
            }
        }
    }
}
