using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace DELFUS.DocGen.Service
{
    public class ServiceBase : Disposable
    {
        protected DbConnection db = null;

        static ServiceBase()
        {
            Handlers.Register();
        }

        protected ServiceBase()
        {
            db = DbFactory.Create("DELFUSLetters");
        }

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        protected override void DisposeManagedObjects()
        {
            if (db != null)
            {
                // Closing a closed connection doesn't make a different so calling this method as a backup
                // in case the connection was kept open.
                db.Close();
                db.Dispose();
                db = null;
            }
        }

        /// <summary>
        /// Free your own state (unmanaged objects).
        /// Set large fields to null.
        /// </summary>
        protected override void DisposeUnManagedObjects()
        {

            // There are no unmanaged resources to free
            return;
        }
    }
}
