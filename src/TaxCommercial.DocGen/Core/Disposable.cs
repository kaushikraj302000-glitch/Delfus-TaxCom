using System;
using System.Collections.Generic;
using System.Text;

namespace TaxCommercial.DocGen
{
    public abstract class Disposable : IDisposable
    {
        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (this.disposed)
                return;

            if (disposing)
            {
                // Dispose managed resources.
                DisposeManagedObjects();
            }

            // Free your own state (unmanaged objects).
            // Set large fields to null.
            DisposeUnManagedObjects();

            // Note disposing has been done.
            disposed = true;
        }

        protected abstract void DisposeManagedObjects();
        protected abstract void DisposeUnManagedObjects();

        ~Disposable()
        {
            Dispose(false);
        }
    }
}
