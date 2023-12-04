using System.Text;

namespace PowerPrompt;

public record AnsiColor(string Foreground, string Background);

public static class StringBuilderExtensions
{
	public static StringBuilder Append(this StringBuilder sb, AnsiColor foreground, AnsiColor background,
		string text) =>
		sb.Append(foreground.Foreground)
			.Append(background.Background)
			.Append(text);
}
