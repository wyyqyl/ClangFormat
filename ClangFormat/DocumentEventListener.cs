using System;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Anonymous.ClangFormat
{
    public class OnBeforeSaveArgs : EventArgs
    {
        public Document Document { get; set; }
        public OnBeforeSaveArgs(Document document)
        {
            Document = document;
        }
    }

    public class DocumentEventListener : IDisposable, IVsRunningDocTableEvents3
    {
        private ClangFormatPackage package_;
        private RunningDocumentTable table_;
        private uint cookie_;
        private bool is_disposed_;

        public delegate void OnBeforeSaveHandler(object sender, OnBeforeSaveArgs e);
        public event OnBeforeSaveHandler BeforeSave;

        public DocumentEventListener(ClangFormatPackage package)
        {
            is_disposed_ = false;
            package_ = package;
            table_ = new RunningDocumentTable(package);
            cookie_ = table_.Advise(this);
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        private Document GetDocumentFromCookie(uint docCookie)
        {
            // Retrieve document information from the cookie to get the full document name.
            var documentName = table_.GetDocumentInfo(docCookie).Moniker;

            // Search against the IDE documents to find the object that matches the full document name.
            return package_.IDE.Documents.OfType<Document>().FirstOrDefault(x => x.FullName == documentName);
        }

        public int OnBeforeSave(uint docCookie)
        {
            if (BeforeSave != null)
            {
                Document document = GetDocumentFromCookie(docCookie);
                BeforeSave(this, new OnBeforeSaveArgs(document));
            }
            return VSConstants.S_OK;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!is_disposed_)
            {
                is_disposed_ = true;
                if (disposing && table_ != null && cookie_ != 0)
                {
                    table_.Unadvise(cookie_);
                    cookie_ = 0;
                }
            }
        }
    }
}
