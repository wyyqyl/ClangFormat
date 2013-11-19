using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Anonymous.ClangFormat
{
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [CLSCompliant(false), ComVisible(true)]
    public class OptionPageGrid : DialogPage
    {
        private string style = "File";
        private bool formatOnSave = true;

        [Category("LLVM/Clang")]
        [DisplayName("Style")]
        [Description("Coding style, currently supports:\n" +
                        "  - Predefined styles ('LLVM', 'Google', 'Chromium', 'Mozilla').\n" +
                        "  - 'File' to search for a YAML .clang-format or _clang-format\n" +
                        "    configuration file.\n" +
                        "  - A YAML configuration snippet.\n\n" +
                        "'File':\n" +
                        "  Searches for a .clang-format or _clang-format configuration file\n" +
                        "  in the source file's directory and its parents.\n\n" +
                        "YAML configuration snippet:\n" +
                        "  The content of a .clang-format configuration file, as string.\n" +
                        "  Example: '{BasedOnStyle: \"LLVM\", IndentWidth: 8}'\n\n" +
                        "See also: http://clang.llvm.org/docs/ClangFormatStyleOptions.html.")]
        public string Style
        {
            get { return style; }
            set { style = value; }
        }

        [Category("LLVM/Clang")]
        [DisplayName("Format on save")]
        public bool FormatOnSave
        {
            get { return formatOnSave; }
            set { formatOnSave = value; }
        }
    }
}
