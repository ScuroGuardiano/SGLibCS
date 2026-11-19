# Syntax highlighting

MdLive's default pipeline (`MdLiveConfigurator.CreateMarkdownPipeline`) highlights fenced code blocks
with [ColorCode](https://github.com/CommunityToolkit/ColorCode-Universal) through
[Markdown.ColorCode](https://github.com/wbaldoumas/markdown-colorcode), using `HtmlFormatterType.Css`.
That formatter emits **only CSS class names, no colors**. MdLive doesn't ship a stylesheet for them,
so code blocks stay uncolored until you add styles, for example to `.mdlive/style.css`.

A future `mdlive init` command will create the `.mdlive` folder with the
[default stylesheet](#default-stylesheet) from this page, so you can tweak it to match your site.

## Generated HTML

A fenced block with a [supported language](#supported-languages):

````markdown
```csharp
var answer = 42; // comment
```
````

becomes a wrapper `div` with the language class, a plain `pre` and a `span` per token:

```html
<div class="csharp"><pre>
<span class="keyword">var</span> answer = <span class="number">42</span>; <span class="comment">// comment</span>
</pre></div>
```

- Token text is HTML-encoded.
- Spans can be nested, when ColorCode captures a token inside another one.
- A few internal scopes have no class and produce `<span class="">` (see [CSS classes](#css-classes)).

Blocks that ColorCode doesn't handle are rendered by the standard Markdig renderer and are **not highlighted**:

- fenced blocks without a language, or with a language ColorCode doesn't know,
- indented code blocks.

They look like `<pre><code class="language-xyz">…</code></pre>`, the class is there only when the fence has an info string.

## Supported languages

The first word of the fence info string is matched case-insensitively against the language id and its aliases.

| Language | Fence info string | Wrapper class |
|---|---|---|
| ASAX | `asax` | `asax` |
| ASHX | `ashx` | `ashx` |
| ASPX | `aspx` | `aspx` |
| ASPX (C#) | `aspx(c#)`, `aspx-cs`, `aspx(cs)` | `aspx-cs` |
| ASPX (VB.NET) | `aspx(vb.net)`, `aspx-vb` | `aspx-vb` |
| C# | `c#`, `cs`, `csharp`, `cake` | `csharp` |
| C++ | `cpp`, `c++`, `c` | `cplusplus` |
| CSS | `css` | `css` |
| F# | `f#`, `fs`, `fsharp` | `FSharp` |
| Fortran | `fortran` | `fortran` |
| Haskell | `haskell`, `hs` | `haskell` |
| HTML | `html`, `htm` | `html` |
| Java | `java` | `java` |
| JavaScript | `javascript`, `js` | `javascript` |
| JSON | `json` | `json` |
| Koka | `koka`, `kk`, `kki` | `koka` |
| Markdown | `markdown`, `md` | `markdown` |
| MATLAB | `matlab`, `m`, `mat` | `matlab` |
| PHP | `php`, `php3`, `php4`, `php5` | `php` |
| PowerShell | `powershell`, `posh`, `ps1`, `pwsh` | `powershell` |
| Python | `python`, `py` | `python` |
| SQL | `sql` | `sql` |
| Typescript | `typescript`, `ts` | `typescript` |
| VB.NET | `vb.net`, `vbnet`, `vb`, `visualbasic` | `vb-net` |
| XML | `xml`, `xaml`, `axml` | `xml` |

Notably **not supported**: `sh` / `bash`, Lua, YAML, TOML, Rust, Go, Dockerfile and diff.
Such blocks fall back to the standard, unhighlighted output described above.

## CSS classes

Class names come from ColorCode's style dictionaries and are the same in both built-in themes,
so the stylesheet doesn't depend on which theme the pipeline was created with.
The color columns show ColorCode's built-in `StyleDictionary.DefaultLight` and `StyleDictionary.DefaultDark`
as a starting point; `–` means the theme doesn't style that class.

| Klasa | Zakres ColorCode | DefaultLight | DefaultDark |
|---|---|---|---|
| `.plainText` | Plain Text | `#000000`, tło `#FFFFFF` | `#DADADA`, tło `#1E1E1E` |
| `.htmlServerSideScript` | HTML Server-Side Script | tło `#FFFF00` | tło `#FFFF00` |
| `.htmlComment` | HTML Comment | `#008000` | `#57A64A` |
| `.htmlTagDelimiter` | Html Tag Delimiter | `#0000FF` | `#569CD6` |
| `.htmlElementName` | HTML Element ScopeName | `#A31515` | `#A31515` |
| `.htmlAttributeName` | HTML Attribute ScopeName | `#FF0000` | `#FF0000` |
| `.htmlAttributeValue` | HTML Attribute Value | `#0000FF` | `#569CD6` |
| `.htmlOperator` | HTML Operator | `#0000FF` | `#569CD6` |
| `.comment` | Comment | `#008000` | `#57A64A` |
| `.xmlDocTag` | XML Doc Tag | `#808080` | `#608B4E` |
| `.xmlDocComment` | XML Doc Comment | `#008000` | `#608B4E` |
| `.string` | String | `#A31515` | `#D69D85` |
| `.stringCSharpVerbatim` | String (C# @ Verbatim) | `#A31515` | `#D69D85` |
| `.keyword` | Keyword | `#0000FF` | `#569CD6` |
| `.preprocessorKeyword` | Preprocessor Keyword | `#0000FF` | `#569CD6` |
| `.htmlEntity` | HTML Entity | `#FF0000` | `#FF0000` |
| `.jsonKey` | Json Key | `#FF8700` | `#FF8700` |
| `.jsonString` | Json String | `#008B8B` | `#008B8B` |
| `.jsonNumber` | Json Number | `#00d700` | `#00d700` |
| `.jsonConst` | Json Const | `#af87ff` | `#af87ff` |
| `.xmlAttribute` | XML Attribute | `#FF0000` | `#92CAF4` |
| `.xmlAttributeQuotes` | XML Attribute Quotes | `#000000` | `#569CD6` |
| `.xmlAttributeValue` | XML Attribute Value | `#0000FF` | `#569CD6` |
| `.xmlCDataSection` | XML CData Section | `#808080` | `#C0D088` |
| `.xmlComment` | XML Comment | `#008000` | `#57A64A` |
| `.xmlDelimiter` | XML Delimiter | `#0000FF` | `#808080` |
| `.xmlName` | XML Name | `#A31515` | `#E6E6E6` |
| `.className` | Class Name | `#48D1CC` | `#4EC9B0` |
| `.cssSelector` | CSS Selector | `#A31515` | `#A31515` |
| `.cssPropertyName` | CSS Property Name | `#FF0000` | `#FF0000` |
| `.cssPropertyValue` | CSS Property Value | `#0000FF` | `#569CD6` |
| `.sqlSystemFunction` | SQL System Function | `#FF00FF` | `#FF00FF` |
| `.powershellAttribute` | PowerShell Attribute | `#B0E0E6` | `#B0E0E6` |
| `.powershellOperator` | PowerShell Operator | `#808080` | `#9B9B9B` |
| `.powershellType` | PowerShell Type | `#008080` | `#008080` |
| `.powershellVariable` | PowerShell Variable | `#FF4500` | `#FF4500` |
| `.powershellCommand` | PowerShell Command | `#000080` | `#FFFF00` |
| `.powershellParameter` | PowerShell Parameter | `#808080` | `#9B9B9B` |
| `.type` | Type | `#008080` | `#008080` |
| `.typeVariable` | Type Variable | `#008080`, kursywa | `#008080`, kursywa |
| `.namespace` | Name Space | `#000080` | `#000080` |
| `.constructor` | Constructor | `#800080` | `#800080` |
| `.predefined` | Predefined | `#000080` | `#000080` |
| `.pseudoKeyword` | Pseudo Keyword | `#000080` | `#000080` |
| `.stringEscape` | String Escape | `#808080` | `#9B9B9B` |
| `.controlKeyword` | Control Keyword | `#0000FF` | `#569CD6` |
| `.number` | Number | – | `#B5CEA8` |
| `.operator` | Operator | – | – |
| `.delimiter` | Delimiter | – | – |
| `.markdownHeader` | Markdown Header | `#0000FF`, pogrubienie | `#569CD6`, pogrubienie |
| `.markdownCode` | Markdown Code | `#008080` | `#D69D85` |
| `.markdownListItem` | Markdown List Item | pogrubienie | pogrubienie |
| `.italic` | Markdown Emphasized | kursywa | kursywa |
| `.bold` | Markdown Bold | pogrubienie | pogrubienie |
| `.builtinFunction` | Built In Function | `#6B8E23`, pogrubienie | `#6B8E23`, pogrubienie |
| `.builtinValue` | Built In Value | `#556B2F`, pogrubienie | `#556B2F`, pogrubienie |
| `.attribute` | Attribute | `#008B8B`, kursywa | `#008B8B`, kursywa |
| `.specialChar` | Special Character | – | – |

Scopes `Brackets`, `Continuation`, `Intrinsic` and the language prefix have no class
and are rendered as `<span class="">`.

## Caveats

- **Scope your selectors to code blocks.** Class names are generic (`bold`, `italic`, `comment`,
  `string`, `keyword`, `attribute`…) and would collide with styles of the document itself.
  Use `pre .keyword`, or `.csharp pre .keyword` for per-language rules.
- **Don't use ColorCode's `HtmlClassFormatter.GetCSSString()`.** Besides the token classes it sets
  the background of the whole `body`, and it writes token backgrounds as `color` instead of `background-color`.
- **Highlighted blocks have no `data-line` attribute.** Markdown.ColorCode writes the formatter's
  HTML as is, without the block's attributes, so editor scroll sync skips over them.
  Unhighlighted blocks keep `data-line`.

## Default stylesheet

ColorCode's `DefaultLight` theme as CSS, scoped to `pre`.
Rules without properties are classes the theme doesn't style, left in as hooks.

```css
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
```
