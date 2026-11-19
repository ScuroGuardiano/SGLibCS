namespace ScuroGuardiano.MdLive.Commands;

internal static class DefaultFiles
{
    /// <summary>
    /// Default <c>.mdlive/style.css</c>: space for user styles followed by ColorCode's DefaultLight theme,
    /// the same stylesheet as in <c>docs/syntax-highlight.md</c>.
    /// </summary>
    public const string StyleCss =
        // language=css
        """
        /* add your styles here */


        /* ###### Highlight classes ###### */
        pre .plainText { color: #000000; background-color: #FFFFFF; }
        pre .htmlServerSideScript { background-color: #FFFF00; }
        pre .htmlComment { color: #008000; }
        pre .htmlTagDelimiter { color: #0000FF; }
        pre .htmlElementName { color: #A31515; }
        pre .htmlAttributeName { color: #FF0000; }
        pre .htmlAttributeValue { color: #0000FF; }
        pre .htmlOperator { color: #0000FF; }
        pre .comment { color: #008000; }
        pre .xmlDocTag { color: #808080; }
        pre .xmlDocComment { color: #008000; }
        pre .string { color: #A31515; }
        pre .stringCSharpVerbatim { color: #A31515; }
        pre .keyword { color: #0000FF; }
        pre .preprocessorKeyword { color: #0000FF; }
        pre .htmlEntity { color: #FF0000; }
        pre .jsonKey { color: #FF8700; }
        pre .jsonString { color: #008B8B; }
        pre .jsonNumber { color: #00d700; }
        pre .jsonConst { color: #af87ff; }
        pre .xmlAttribute { color: #FF0000; }
        pre .xmlAttributeQuotes { color: #000000; }
        pre .xmlAttributeValue { color: #0000FF; }
        pre .xmlCDataSection { color: #808080; }
        pre .xmlComment { color: #008000; }
        pre .xmlDelimiter { color: #0000FF; }
        pre .xmlName { color: #A31515; }
        pre .className { color: #48D1CC; }
        pre .cssSelector { color: #A31515; }
        pre .cssPropertyName { color: #FF0000; }
        pre .cssPropertyValue { color: #0000FF; }
        pre .sqlSystemFunction { color: #FF00FF; }
        pre .powershellAttribute { color: #B0E0E6; }
        pre .powershellOperator { color: #808080; }
        pre .powershellType { color: #008080; }
        pre .powershellVariable { color: #FF4500; }
        pre .powershellCommand { color: #000080; }
        pre .powershellParameter { color: #808080; }
        pre .type { color: #008080; }
        pre .typeVariable { color: #008080; font-style: italic; }
        pre .namespace { color: #000080; }
        pre .constructor { color: #800080; }
        pre .predefined { color: #000080; }
        pre .pseudoKeyword { color: #000080; }
        pre .stringEscape { color: #808080; }
        pre .controlKeyword { color: #0000FF; }
        pre .number { }
        pre .operator { }
        pre .delimiter { }
        pre .markdownHeader { color: #0000FF; font-weight: bold; }
        pre .markdownCode { color: #008080; }
        pre .markdownListItem { font-weight: bold; }
        pre .italic { font-style: italic; }
        pre .bold { font-weight: bold; }
        pre .builtinFunction { color: #6B8E23; font-weight: bold; }
        pre .builtinValue { color: #556B2F; font-weight: bold; }
        pre .attribute { color: #008B8B; font-style: italic; }
        pre .specialChar { }

        """;
}
