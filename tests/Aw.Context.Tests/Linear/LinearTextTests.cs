using Aw.Context.Infrastructure.Linear;

namespace Aw.Context.Tests.Linear;

/// <summary>The stored text of a Linear issue: every attachment and every image link survives (owner q105, 2026-10-24).</summary>
public sealed class LinearTextTests
{
    private const string Pic = "https://uploads.linear.app/ws/abc/shot.png";

    private static LinearIssue Issue(string? description = null, IReadOnlyList<LinearComment>? comments = null, IReadOnlyList<LinearAttachment>? attachments = null, bool moreAttachments = false) =>
        new("id", "SPL-1", "Title", DateTimeOffset.UnixEpoch, description,
            Comments: comments is null ? null : new LinearConnection<LinearComment>(comments),
            Attachments: attachments is null ? null : new LinearConnection<LinearAttachment>(attachments, new LinearPageInfo(moreAttachments)));

    private static LinearComment Comment(string body) => new("c", body, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Slack_figma_file_and_plain_url_attachments_are_kept()
    {
        var text = LinearText.Of(Issue(attachments:
        [
            new("1", "https://acme.slack.com/archives/C1/p1", "Thread", "slack"),
            new("2", "https://www.figma.com/file/x", "Mock", "figma"),
            new("3", "https://files.example.com/spec.pdf", "Spec", "file"),
            new("4", "https://example.com/page", "Page"),
        ]));

        Assert.Contains("\n\nAttachments:\n", text, StringComparison.Ordinal);
        Assert.Contains("- Thread (slack) https://acme.slack.com/archives/C1/p1", text, StringComparison.Ordinal);
        Assert.Contains("- Mock (figma) https://www.figma.com/file/x", text, StringComparison.Ordinal);
        Assert.Contains("- Spec (file) https://files.example.com/spec.pdf", text, StringComparison.Ordinal);
        Assert.Contains("- Page (link) https://example.com/page", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_title_falls_back_to_the_host()
    {
        var text = LinearText.Of(Issue(attachments: [new("1", "https://www.figma.com/file/x", null, "figma")]));

        Assert.Contains("- www.figma.com (figma) https://www.figma.com/file/x", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_pull_request_attachment_is_kept_with_its_source_type()
    {
        var text = LinearText.Of(Issue(attachments: [new("1", "https://github.com/o/r/pull/5", "Fix", "github")]));

        Assert.Contains("- Fix (github) https://github.com/o/r/pull/5", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_more_attachments_marker_stays()
    {
        var text = LinearText.Of(Issue(attachments: [new("1", "https://example.com/a", "A")], moreAttachments: true));

        Assert.EndsWith("(more attachments not shown)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_markdown_image_in_the_description_is_listed_with_its_alt_text_and_link()
    {
        var text = LinearText.Of(Issue(description: $"See ![the crash]({Pic}) here"));

        Assert.Contains($"\n\nImages:\n- the crash {Pic}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_html_image_in_a_comment_is_listed()
    {
        var text = LinearText.Of(Issue(comments: [Comment($"look <img src=\"{Pic}\" alt=\"error dialog\"> ok")]));

        Assert.Contains($"\n\nImages:\n- error dialog {Pic}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_image_without_alt_text_is_called_image()
    {
        var text = LinearText.Of(Issue(comments: [Comment($"![]({Pic})")]));

        Assert.Contains($"- image {Pic}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_image_attachment_is_listed_by_extension_or_by_the_uploads_host()
    {
        var text = LinearText.Of(Issue(attachments:
        [
            new("1", "https://cdn.example.com/a.JPG?x=1", "Photo", "file"),
            new("2", "https://uploads.linear.app/ws/def/blob", "Upload", "file"),
            new("3", "https://example.com/doc.pdf", "Doc", "file"),
        ]));

        var images = text[text.IndexOf("Images:", StringComparison.Ordinal)..];
        Assert.Contains("- Photo https://cdn.example.com/a.JPG?x=1", images, StringComparison.Ordinal);
        Assert.Contains("- Upload https://uploads.linear.app/ws/def/blob", images, StringComparison.Ordinal);
        Assert.DoesNotContain("doc.pdf", images, StringComparison.Ordinal);
    }

    [Fact]
    public void An_image_found_twice_is_listed_once()
    {
        var text = LinearText.Of(Issue(description: $"![a]({Pic})", comments: [Comment($"![b]({Pic})")]));

        Assert.Equal(1, text.Split("\n- ", StringSplitOptions.None).Count(p => p.Contains(Pic, StringComparison.Ordinal) && p.StartsWith("a ", StringComparison.Ordinal)));
        var images = text[text.IndexOf("Images:", StringComparison.Ordinal)..];
        Assert.Single(images.Split('\n'), l => l.StartsWith("- ", StringComparison.Ordinal));
    }

    [Fact]
    public void A_text_without_images_has_no_images_section()
    {
        Assert.DoesNotContain("Images:", LinearText.Of(Issue(description: "plain")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_64_kb_cut_still_holds()
    {
        var text = LinearText.Of(Issue(description: new string('x', 70_000), attachments: [new("1", "https://example.com/a", "A")]));

        Assert.Equal(64 * 1024, System.Text.Encoding.UTF8.GetByteCount(text));
    }

    [Fact]
    public void A_long_description_is_cut_and_every_attachment_and_image_line_survives()
    {
        var text = LinearText.Of(Issue(description: new string('x', 70_000) + $" ![a]({Pic})", comments: [Comment("late comment")], attachments:
        [
            new("1", "https://acme.slack.com/archives/C1/p1", "Thread", "slack"),
            new("2", "https://example.com/shot.png", "Shot", "file"),
        ]));

        Assert.True(System.Text.Encoding.UTF8.GetByteCount(text) <= 64 * 1024);
        Assert.Contains("- Thread (slack) https://acme.slack.com/archives/C1/p1", text, StringComparison.Ordinal);
        Assert.Contains("- Shot (file) https://example.com/shot.png", text, StringComparison.Ordinal);
        Assert.Contains("\n\nImages:\n- a " + Pic + "\n- Shot https://example.com/shot.png", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Sections_over_64_kb_are_cut_at_a_line_with_a_mark()
    {
        var attachments = Enumerable.Range(0, 3000).Select(n => new LinearAttachment(n.ToString(System.Globalization.CultureInfo.InvariantCulture), $"https://example.com/item/{n}", "Item " + n, "link")).ToList();

        var text = LinearText.Of(Issue(description: "desc", attachments: attachments));

        Assert.True(System.Text.Encoding.UTF8.GetByteCount(text) <= 64 * 1024);
        Assert.EndsWith("\n(more not shown)", text, StringComparison.Ordinal);
        var lines = text.Split('\n');
        Assert.Matches("^- Item \\d+ \\(link\\) https://example.com/item/\\d+$", lines[^2]);
    }
}
