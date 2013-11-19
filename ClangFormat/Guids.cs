// Guids.cs
// MUST match guids.h
using System;

namespace Anonymous.ClangFormat
{
    static class GuidList
    {
        public const string guidClangFormatPkgString = "f8111347-1024-4a99-b2b0-e0271fb5b5f5";
        public const string guidClangFormatCmdSetString = "d0231aba-fe3a-4d06-9213-5000ef99ca08";

        public static readonly Guid guidClangFormatCmdSet = new Guid(guidClangFormatCmdSetString);
    };
}