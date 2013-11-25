// Guids.cs
// MUST match guids.h
using System;

namespace Anonymous.ClangFormat
{
    static class GuidList
    {
        public const string guidClangFormatPkgString = "bff0e311-a5e8-45fb-9eca-aba2a1f6b075";
        public const string guidClangFormatCmdSetString = "f35d3c3c-7506-4857-92e6-031f83e82f6f";

        public static readonly Guid guidClangFormatCmdSet = new Guid(guidClangFormatCmdSetString);
    };
}