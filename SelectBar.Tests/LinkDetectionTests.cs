using System;
using System.Collections.Generic;
using SelectBar.Actions;
using Xunit;

namespace SelectBar.Tests;

public class LinkDetectionTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://a.fr/x?y=1#z")]
    [InlineData("HTTPS://EXAMPLE.COM")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://203.0.113.1/admin")]
    [InlineData("www.exemple.com/x")]
    [InlineData("exemple.com")]
    [InlineData("sub.exemple.co")]
    [InlineData("exemple.com:8080/x")]
    [InlineData("mailto:a@b.fr")]
    [InlineData("file:///C:/Temp/a.txt")]
    [InlineData("<https://a.fr/x>")]
    [InlineData("\"https://a.fr\"")]
    [InlineData("(https://a.fr)")]
    [InlineData("https://a.fr.")]
    [InlineData("https://a.fr/x,")]
    [InlineData("https://a.fr/x;")]
    [InlineData("https://a.fr/x!")]
    [InlineData("https://a.fr/x?")]
    [InlineData("  https://a.fr  ")]
    [InlineData("https://en.wikipedia.org/wiki/Foo_(bar)")]
    public void IsUrl_valid(string text) => Assert.True(TextClassifier.IsUrl(text), text);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello")]
    [InlineData("hello world.com")]
    [InlineData("report.pdf")]
    [InlineData("file.txt")]
    [InlineData("v1.2.3")]
    [InlineData("1.2.3.4")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://x.fr")]
    [InlineData("http://")]
    [InlineData("https://")]
    [InlineData("a@b.fr")]
    [InlineData("C:\\Temp\\a.txt")]
    [InlineData("mailto:")]
    [InlineData("a b")]
    public void IsUrl_invalid(string text) => Assert.False(TextClassifier.IsUrl(text), text);

    [Theory]
    [InlineData("Voir https://exemple.com/page, merci", "https://exemple.com/page")]
    [InlineData("Va sur www.exemple.com.", "www.exemple.com")]
    [InlineData("Lis (https://a.fr/x) puis", "https://a.fr/x")]
    [InlineData("<https://a.fr/y> et https://b.fr", "https://a.fr/y")]
    [InlineData("http://a.fr et http://b.fr", "http://a.fr")]
    [InlineData("visit HTTP://A.FR/X now", "HTTP://A.FR/X")]
    [InlineData("« https://a.fr/z »", "https://a.fr/z")]
    [InlineData("exemple.com", "exemple.com")]
    [InlineData("https://en.wikipedia.org/wiki/Foo_(bar).", "https://en.wikipedia.org/wiki/Foo_(bar)")]
    public void FindUrl_finds_first(string text, string expected) => Assert.Equal(expected, TextClassifier.FindUrl(text));

    [Theory]
    [InlineData("pas de lien ici")]
    [InlineData("exemple.com au milieu d'une phrase")]
    [InlineData("hello world.com")]
    [InlineData("")]
    public void FindUrl_none(string text) => Assert.Null(TextClassifier.FindUrl(text));

    [Theory]
    [InlineData("example.com", "https://example.com")]
    [InlineData("www.exemple.com/x", "https://www.exemple.com/x")]
    [InlineData("https://a.fr", "https://a.fr")]
    [InlineData("http://a.fr/x", "http://a.fr/x")]
    [InlineData("mailto:a@b.fr", "mailto:a@b.fr")]
    [InlineData("<https://a.fr/x>.", "https://a.fr/x")]
    [InlineData("Voir www.exemple.com/x, merci", "https://www.exemple.com/x")]
    [InlineData("exemple.com:8080/x", "https://exemple.com:8080/x")]
    public void NormalizeUrl(string input, string expected) => Assert.Equal(expected, TextClassifier.NormalizeUrl(input));

    [Theory]
    [InlineData("jean@exemple.fr", "jean@exemple.fr")]
    [InlineData("mailto:jean@exemple.fr", "jean@exemple.fr")]
    [InlineData("mailto:jean@exemple.fr?subject=x", "jean@exemple.fr")]
    [InlineData("<jean.dupont+tag@sub.exemple.co.uk>", "jean.dupont+tag@sub.exemple.co.uk")]
    [InlineData("Écris à jean@exemple.fr.", "jean@exemple.fr")]
    [InlineData("contact: jean@exemple.fr, merci", "jean@exemple.fr")]
    public void FindEmail_valid(string text, string expected) => Assert.Equal(expected, TextClassifier.FindEmail(text));

    [Theory]
    [InlineData("jean@")]
    [InlineData("@exemple.fr")]
    [InlineData("jean@exemple")]
    [InlineData("a@b.c")]
    [InlineData("pas un mail")]
    [InlineData("")]
    public void FindEmail_invalid(string text) => Assert.Null(TextClassifier.FindEmail(text));

    [Fact]
    public void FindEmail_ignored_inside_sentence_with_web_link() =>
        Assert.Null(TextClassifier.FindEmail("https://exemple.fr écrit à jean@exemple.fr", webLinkPresent: true));

    // ---------------------------------------------------------------- chemins

    private static string? Env(string name) => name.ToUpperInvariant() switch
    {
        "USERPROFILE" => @"C:\Users\jean",
        "APPDATA" => @"C:\Users\jean\AppData\Roaming",
        _ => null
    };

    private const string Home = @"C:\Users\jean";

    [Theory]
    [InlineData(@"C:\Users\x\f.txt", @"C:\Users\x\f.txt")]
    [InlineData("C:/x/y", @"C:\x\y")]
    [InlineData(@"c:\temp", @"c:\temp")]
    [InlineData(@"\\serveur\partage\dossier", @"\\serveur\partage\dossier")]
    [InlineData("//serveur/partage/dossier", @"\\serveur\partage\dossier")]
    [InlineData(@"\\serveur\partage", @"\\serveur\partage")]
    [InlineData(@"%USERPROFILE%\Desktop", @"C:\Users\jean\Desktop")]
    [InlineData(@"%appdata%\Foo\bar.txt", @"C:\Users\jean\AppData\Roaming\Foo\bar.txt")]
    [InlineData(@"~\Documents", @"C:\Users\jean\Documents")]
    [InlineData("~/Documents", @"C:\Users\jean\Documents")]
    [InlineData("~", @"C:\Users\jean")]
    [InlineData("\"C:\\Program Files\\App\\a b.txt\"", @"C:\Program Files\App\a b.txt")]
    [InlineData("'C:\\Temp\\a.txt'", @"C:\Temp\a.txt")]
    [InlineData("<C:\\Temp\\a.txt>", @"C:\Temp\a.txt")]
    [InlineData(@"C:\Program Files (x86)\App", @"C:\Program Files (x86)\App")]
    [InlineData("  C:\\Temp\\a.txt  ", @"C:\Temp\a.txt")]
    [InlineData("file:///C:/Users/x/a%20b.txt", @"C:\Users\x\a b.txt")]
    [InlineData("file://serveur/partage/x.txt", @"\\serveur\partage\x.txt")]
    [InlineData("file://localhost/C:/Temp/a.txt", @"C:\Temp\a.txt")]
    [InlineData(@"\\?\C:\Very\Long\path.txt", @"\\?\C:\Very\Long\path.txt")]
    [InlineData(@"\\?\UNC\srv\share\x", @"\\?\UNC\srv\share\x")]
    public void ResolvePath_valid(string text, string expected) =>
        Assert.Equal(expected, TextClassifier.ResolvePath(text, Env, Home));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a path")]
    [InlineData("C:")]
    [InlineData("C:file.txt")]
    [InlineData(@"relatif\chemin")]
    [InlineData(@"C:\a<b")]
    [InlineData(@"C:\a|b")]
    [InlineData(@"C:\a?b")]
    [InlineData(@"C:\a*b")]
    [InlineData("C:\\a\"b")]
    [InlineData(@"C:\file.txt:stream")]
    [InlineData(@"C:\CON")]
    [InlineData(@"C:\dir\NUL.txt")]
    [InlineData(@"C:\dir\com1")]
    [InlineData(@"\\.\PhysicalDrive0")]
    [InlineData(@"\\server")]
    [InlineData(@"\\.\share\x")]
    [InlineData(@"%UNKNOWN%\x")]
    [InlineData("C:\\a\nC:\\b")]
    [InlineData("file:///")]
    [InlineData("file://host")]
    [InlineData("https://exemple.com")]
    public void ResolvePath_invalid(string text) => Assert.Null(TextClassifier.ResolvePath(text, Env, Home));

    [Fact]
    public void ResolvePath_length_limits()
    {
        Assert.NotNull(TextClassifier.ResolvePath(@"C:\" + new string('a', 257), Env, Home));  // 260
        Assert.Null(TextClassifier.ResolvePath(@"C:\" + new string('a', 258), Env, Home));     // 261
        Assert.NotNull(TextClassifier.ResolvePath(@"\\?\C:\" + new string('a', 300), Env, Home));
    }

    [Theory]
    [InlineData(@"C:\Temp\a.txt", @"C:\Temp")]
    [InlineData(@"C:\Temp", @"C:\")]
    [InlineData(@"C:\Temp\", @"C:\")]
    [InlineData(@"\\srv\share\a\b.txt", @"\\srv\share\a")]
    [InlineData(@"\\srv\share\a", @"\\srv\share")]
    [InlineData(@"\\?\C:\a\b", @"\\?\C:\a")]
    public void ParentOf(string path, string expected) => Assert.Equal(expected, TextClassifier.ParentOf(path));

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"\\srv\share")]
    [InlineData(@"\\srv\share\")]
    [InlineData("nope")]
    public void ParentOf_none(string path) => Assert.Null(TextClassifier.ParentOf(path));

    [Fact]
    public void ResolveExisting_file_parent_or_nothing()
    {
        var existing = new HashSet<string> { @"C:\Temp\a.txt", @"C:\Temp" };
        bool Exists(string p) => existing.Contains(p);
        Assert.Equal(@"C:\Temp\a.txt", TextClassifier.ResolveExisting(@"C:\Temp\a.txt", Exists));
        Assert.Equal(@"C:\Temp\missing.txt", TextClassifier.ResolveExisting(@"C:\Temp\missing.txt", Exists)); // le dossier parent existe
        Assert.Null(TextClassifier.ResolveExisting(@"C:\Nope\missing.txt", Exists));
        Assert.Null(TextClassifier.ResolveExisting("pas un chemin", _ => true));
        Assert.Null(TextClassifier.ResolveExisting(@"C:\Temp\a.txt", _ => throw new System.IO.IOException()));
    }

    [Fact]
    public void IsPath_legacy_behaviour()
    {
        Assert.True(TextClassifier.IsPath(@"C:\Users\me\file.txt", _ => true));
        Assert.False(TextClassifier.IsPath(@"C:\Users\me\file.txt", _ => false));
        Assert.True(TextClassifier.IsPath("\"C:\\Temp\\a b.txt\"", _ => true));
        Assert.True(TextClassifier.IsPath(@"\\server\share\x", _ => true));
        Assert.False(TextClassifier.IsPath("not a path", _ => true));
    }

    // ---------------------------------------------------------------- contexte

    private static ActionContext Ctx(string text, Func<string, bool>? exists = null) =>
        TextClassifier.BuildContext(text, false, "notepad.exe", exists ?? (_ => false));

    [Fact]
    public void Context_url()
    {
        var c = Ctx("https://a.fr");
        Assert.True(c.IsUrl); Assert.Equal("https://a.fr", c.LinkTarget);
        Assert.False(c.IsEmail); Assert.False(c.IsPath);
    }

    [Fact]
    public void Context_url_inside_sentence()
    {
        var c = Ctx("Voir www.exemple.com/x, merci");
        Assert.True(c.IsUrl);
        Assert.Equal("www.exemple.com/x", c.LinkTarget);
    }

    [Fact]
    public void Context_email_and_mailto()
    {
        var c = Ctx("contact: jean@exemple.fr");
        Assert.True(c.IsEmail); Assert.Equal("mailto:jean@exemple.fr", c.MailTarget); Assert.False(c.IsUrl);
        var m = Ctx("mailto:a@b.fr");
        Assert.True(m.IsEmail); Assert.Equal("mailto:a@b.fr", m.MailTarget); Assert.False(m.IsUrl);
    }

    [Fact]
    public void Context_url_and_email_in_same_sentence_keeps_the_link()
    {
        var c = Ctx("https://a.fr et jean@exemple.fr");
        Assert.True(c.IsUrl); Assert.False(c.IsEmail);
    }

    [Fact]
    public void Context_path_exists_parent_exists_or_not()
    {
        var c1 = Ctx(@"C:\Temp\a.txt", p => p == @"C:\Temp\a.txt");
        Assert.True(c1.IsPath); Assert.Equal(@"C:\Temp\a.txt", c1.PathTarget);
        Assert.True(Ctx(@"C:\Temp\a.txt", p => p == @"C:\Temp").IsPath);
        Assert.False(Ctx(@"C:\Temp\a.txt", _ => false).IsPath);
        Assert.Null(Ctx(@"C:\Temp\a.txt", _ => false).PathTarget);
    }

    [Fact]
    public void Context_file_url_is_a_path_not_a_link()
    {
        var c = Ctx("file:///C:/Temp/a.txt", _ => true);
        Assert.True(c.IsPath); Assert.Equal(@"C:\Temp\a.txt", c.PathTarget);
        Assert.False(c.IsUrl);
    }

    [Fact]
    public void Context_plain_text_is_nothing()
    {
        var c = Ctx("bonjour tout le monde");
        Assert.False(c.IsUrl); Assert.False(c.IsEmail); Assert.False(c.IsPath);
    }

    [Theory]
    [InlineData("https://a.fr/x.", "https://a.fr/x")]
    [InlineData("\"quoted\"", "quoted")]
    [InlineData("(sans fin", "sans fin")]
    [InlineData("f(x)", "f(x)")]
    [InlineData("f(x))", "f(x)")]
    [InlineData("...", "")]
    public void CleanToken(string input, string expected) => Assert.Equal(expected, TextClassifier.CleanToken(input));
}
