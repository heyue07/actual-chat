﻿namespace ActualChat.Chat.UnitTests.Markup2;

public class MarkupParser2Test : TestBase
{
    public MarkupParser2Test(ITestOutputHelper @out) : base(@out) { }

    [Fact]
    public void BasicTest()
    {
        var m = Parse<PlainTextMarkup>("Мороз и солнце; день\r\n чудесный!", out var text);
        m.Text.Should().Be(text);

        m = Parse<PlainTextMarkup>(" ", out text);
        m.Text.Should().Be(text);

        m = Parse<PlainTextMarkup>("", out text);
        m.Text.Should().Be(text);
    }

    [Fact]
    public void UrlTest()
    {
        var m = Parse<UrlMarkup>("https://habr.com/ru/all/", out var text);
        m.Url.Should().Be(text);
        m.IsImage.Should().BeFalse();
    }

    [Fact]
    public void MentionTest()
    {
        var m = Parse<Mention>("@alex", out var text);
        m.Target.Should().Be(text[1..]);
        m.Kind.Should().Be(MentionKind.Unknown);

        m = Parse<Mention>("@a:chatId:1", out text);
        m.Target.Should().Be(text[3..]);
        m.Kind.Should().Be(MentionKind.AuthorId);

        m = Parse<Mention>("@u:userId", out text);
        m.Target.Should().Be(text[3..]);
        m.Kind.Should().Be(MentionKind.UserId);
    }

    [Fact]
    public void ImageTest()
    {
        var m = Parse<UrlMarkup>("https://pravlife.org/sites/field/image/13_48.jpg", out var text);
        m.Url.Should().Be(text);
        m.IsImage.Should().BeTrue();
    }

    [Fact]
    public void ItalicTest()
    {
        var m = Parse<StylizedTextMarkup>("*italic text*", out var text);
        m.Style.Should().Be(TextStyle.Italic);
        var m1 = m.Markup.Should().BeOfType<PlainTextMarkup>().Subject;
        m1.Text.Should().Be(text[1..^1]);
    }

    [Fact]
    public void BoldTest()
    {
        var m = Parse<StylizedTextMarkup>("**bold text**", out var text);
        m.Style.Should().Be(TextStyle.Bold);
        var m1 = m.Markup.Should().BeOfType<PlainTextMarkup>().Subject;
        m1.Text.Should().Be(text[2..^2]);
    }

    [Fact]
    public void PreformattedTest()
    {
        var m = Parse<PreformattedTextMarkup>("`a``b`", out _);
        m.Text.Should().Be("a`b");
    }

    [Fact]
    public void CodeTest()
    {
        return;
        var m = Parse<CodeBlockMarkup>(@"```cs
code
```", out _);
        m.Language.Should().Be("cs");
        m.Code.Should().Be("code\r\n");

        m = Parse<CodeBlockMarkup>(@"```cs
```", out _);
        m.Language.Should().Be("cs");
        m.Code.Should().Be("");
    }

    [Fact]
    public void MixedCodeTest()
    {
        var m = Parse<MarkupSeq>(@"
1
```cs
code
```
2", out _);
        m.Items.Length.Should().Be(3);
    }

    [Fact]
    public void UnparsedTest()
    {
        var m = Parse<UnparsedMarkup>("**", out var text);
        m.Text.Should().Be(text);
    }

    [Fact]
    public void MixedTest()
    {
        var m = Parse<MarkupSeq>("***bi*** @alex `a``b` *i* **b** *", out _);
        m.Items.Length.Should().Be(2);
        var um = (UnparsedMarkup) m.Items.Last();
        um.Text.Should().Be("*");
    }

    // Helpers

    private TResult Parse<TResult>(string text, out string copy)
        where TResult : Markup
    {
        copy = text;
        Out.WriteLine($"-> {text}");
        MarkupParser2.Out = Out;
        var parsed = MarkupParser2.Parse(text);
        Out.WriteLine($"<- {parsed}");
        var result = parsed.Should().BeOfType<TResult>().Subject;
        var plainText = parsed.ToPlainText().Replace("\r\n", "\n");
        var expectedPlainText = text.Replace("\r\n", "\n");
        plainText.Should().Be(expectedPlainText);
        return result;
    }
}
