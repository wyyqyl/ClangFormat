using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using EnvDTE;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;
using System.Xml.Linq;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;

namespace Anonymous.ClangFormat
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.guidClangFormatPkgString)]
    [ProvideAutoLoad("{f1536ef8-92ec-443c-9ed7-fdadf150da82}")]
    [ProvideOptionPage(typeof(OptionPageGrid), "LLVM/Clang", "ClangFormat", 0, 0, true)]
    public sealed class ClangFormatPackage : Package
    {
        public DocumentEventListener docEventListener_;
        private DTE _ide;

        #region Package Members
        protected override void Initialize()
        {
            base.Initialize();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                CommandID menuCommandID = new CommandID(GuidList.guidClangFormatCmdSet, (int)PkgCmdIDList.cmdidFormatSelection);
                OleMenuCommand menuItem = new OleMenuCommand(FormatSelectionCallback, menuCommandID);
                menuItem.BeforeQueryStatus += OnBeforeQueryStatus;
                mcs.AddCommand(menuItem);

                menuCommandID = new CommandID(GuidList.guidClangFormatCmdSet, (int)PkgCmdIDList.cmdidFormatDocument);
                menuItem = new OleMenuCommand(FormatDocumentCallback, menuCommandID);
                menuItem.BeforeQueryStatus += OnBeforeQueryStatus;
                mcs.AddCommand(menuItem);
            }

            docEventListener_ = new DocumentEventListener(this);
            docEventListener_.BeforeSave += OnBeforeDocumentSave;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (docEventListener_ != null)
            {
                docEventListener_.Dispose();
            }
        }
        #endregion

        public DTE IDE
        {
            get { return _ide ?? (_ide = (DTE)GetService(typeof(DTE))); }
        }

        private void FormatSelectionCallback(object sender, EventArgs e)
        {
            IWpfTextView view = GetCurrentView();
            if (view == null)
                return;

            int start = view.Selection.Start.Position.GetContainingLine().Start.Position;
            int end = view.Selection.End.Position.GetContainingLine().End.Position;
            FormatSelection(view, start, end - start);
        }

        private void FormatDocumentCallback(object sender, EventArgs e)
        {
            IWpfTextView view = GetCurrentView();
            if (view == null)
                return;

            FormatSelection(view);
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand cmd = (OleMenuCommand)sender;
            if (GetCurrentView() == null)
            {
                cmd.Visible = false;
            }
            else
            {
                cmd.Visible = true;
            }
            cmd.Enabled = cmd.Visible;
        }

        private void OnBeforeDocumentSave(object source, EventArgs args)
        {
            if (((OptionPageGrid)GetDialogPage(typeof(OptionPageGrid))).FormatOnSave)
            {
                IWpfTextView view = GetCurrentView();
                if (view == null)
                    return;

                FormatSelection(view);
            }
        }

        /// <summary>
        /// Runs the given text through clang-format and returns the replacements as XML.
        /// 
        /// Formats the text range starting at offset of the given length.
        /// </summary>
        private string RunClangFormat(string text, int offset, int length, string path)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.FileName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\Resources\clang-format.exe";

            var page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
            // Poor man's escaping - this will not work when quotes are already escaped
            // in the input (but we don't need more).
            string style = page.Style.Replace("\"", "\\\"");
            process.StartInfo.Arguments = " -offset " + offset +
                                          " -length " + length +
                                          " -output-replacements-xml " +
                                          " -style \"" + style + "\"";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            if (path != null)
                process.StartInfo.WorkingDirectory = path;

            // We have to be careful when communicating via standard input / output,
            // as writes to the buffers will block until they are read from the other side.
            // Thus, we:
            // 1. Start the process - clang-format.exe will start to read the input from the
            //    standard input.
            process.Start();

            // 2. We write everything to the standard output - this cannot block, as clang-format
            //    reads the full standard input before analyzing it without writing anything to the
            //    standard output.
            process.StandardInput.Write(text);

            // 3. We notify clang-format that the input is done - after this point clang-format
            //    will start analyzing the input and eventually write the output.
            process.StandardInput.Close();

            // 4. We must read clang-format's output before waiting for it to exit; clang-format
            //    will close the channel by exiting.
            string output = process.StandardOutput.ReadToEnd();

            // 5. clang-format is done, wait until it is fully shut down.
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                // FIXME: If clang-format writes enough to the standard error stream to block,
                // we will never reach this point; instead, read the standard error asynchronously.
                throw new Exception(process.StandardError.ReadToEnd());
            }
            return output;
        }

        private void FormatSelection(IWpfTextView view, int start = 0, int length = -1)
        {
            string text = view.TextBuffer.CurrentSnapshot.GetText();
            if (length == -1)
                length = text.Length;

            if (length == 0)
                return;

            // clang-format doesn't support formatting a range that starts at the end
            // of the file.
            if (start >= text.Length)
                start = text.Length - 1;

            try
            {
                var root = XElement.Parse(RunClangFormat(text, start, length, GetDocumentParent(view)));
                var edit = view.TextBuffer.CreateEdit();
                foreach (XElement replacement in root.Descendants("replacement"))
                {
                    var span = new Span(
                        int.Parse(replacement.Attribute("offset").Value),
                        int.Parse(replacement.Attribute("length").Value));
                    edit.Replace(span, replacement.Value);
                }
                edit.Apply();
            }
            catch (Exception exception)
            {
                var uiShell = (IVsUIShell)GetService(typeof(SVsUIShell));
                var id = Guid.Empty;
                int result;
                uiShell.ShowMessageBox(
                        0, ref id,
                        "Error while running clang-format:",
                        exception.Message,
                        string.Empty, 0,
                        OLEMSGBUTTON.OLEMSGBUTTON_OK,
                        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST,
                        OLEMSGICON.OLEMSGICON_INFO,
                        0, out result);
            }
        }

        /// <summary>
        /// Returns the currently active view if it is a IWpfTextView.
        /// </summary>
        private IWpfTextView GetCurrentView()
        {
            // The SVsTextManager is a service through which we can get the active view.
            var textManager = (IVsTextManager)Package.GetGlobalService(typeof(SVsTextManager));
            IVsTextView textView;
            textManager.GetActiveView(1, null, out textView);

            // Now we have the active view as IVsTextView, but the text interfaces we need
            // are in the IWpfTextView.
            var userData = (IVsUserData)textView;
            if (userData == null)
                return null;

            Guid guidWpfViewHost = DefGuidList.guidIWpfTextViewHost;
            object host;
            userData.GetData(ref guidWpfViewHost, out host);

            IWpfTextViewHost viewHost = (IWpfTextViewHost)host;
            string language = viewHost.TextView.TextDataModel.ContentType.TypeName;
            if (language != "C/C++")
                return null;

            return viewHost.TextView;
        }

        private string GetDocumentParent(IWpfTextView view)
        {
            ITextDocument document;
            if (view.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out document))
            {
                return Directory.GetParent(document.FilePath).ToString();
            }
            return null;
        }
    }
}
