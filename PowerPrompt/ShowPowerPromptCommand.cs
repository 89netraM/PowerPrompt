using System;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace PowerPrompt;

[Cmdlet(VerbsCommon.Show, "PowerPrompt")]
[OutputType(typeof(string))]
public class ShowPowerPromptCommand : PSCmdlet
{
	private static readonly AnsiColor DefaultColor = new("\x1b[39m", "\x1b[49m");
	private static readonly AnsiColor CwdBackground = new("\x1b[34m", "\x1b[44m");
	private static readonly AnsiColor CwdForeground = new("\x1b[97m", "\x1b[107m");
	private static readonly AnsiColor AwsProfileDevBackground = new("\x1b[95m", "\x1b[105m");
	private static readonly AnsiColor AwsProfileQaBackground = new("\x1b[38;5;208m", "\x1b[48;5;208m");
	private static readonly AnsiColor AwsProfileProdBackground = new("\x1b[31m", "\x1b[41m");
	private static readonly AnsiColor AwsProfileForeground = new("\x1b[30m", "\x1b[40m");
	private static readonly AnsiColor GitForeground = new("\x1b[30m", "\x1b[30m");
	private static readonly AnsiColor GitUnknownColor = new("\x1b[90m", "\x1b[100m");
	private static readonly AnsiColor GitOutOfSyncColor = new("\x1b[96m", "\x1b[106m");
	private static readonly AnsiColor GitDirtyColor = new("\x1b[33m", "\x1b[43m");
	private static readonly AnsiColor GitCleanColor = new("\x1b[32m", "\x1b[42m");

	private static readonly StatusOptions StatusOptions = new()
	{
		Show = StatusShowOption.IndexAndWorkDir,
		DetectRenamesInIndex = false,
		DetectRenamesInWorkDir = false,
		ExcludeSubmodules = true,
		RecurseIgnoredDirs = false,
		RecurseUntrackedDirs = false,
		DisablePathSpecMatch = true,
		IncludeUnaltered = false,
		IncludeIgnored = false,
		IncludeUntracked = true,
	};

	protected override void ProcessRecord()
	{
		var promptStart = Host.UI.RawUI.CursorPosition;
		var repository = GetRepository();
		var prompt = ConstructPrompt(repository, null);
		WriteObject(prompt);

		if (repository is not null)
		{
			Task.Run(() =>
			{
				var status = repository.RetrieveStatus(StatusOptions);
				if (status is null)
				{
					return;
				}

				var newPrompt = ConstructPrompt(repository, status);
				var cursor = Host.UI.RawUI.CursorPosition;
				Host.UI.RawUI.CursorPosition = promptStart;
				Host.UI.Write(newPrompt);
				Host.UI.RawUI.CursorPosition = cursor;
			});
		}
	}

	private Repository? GetRepository()
	{
		var repoPath = Repository.Discover(SessionState.Path.CurrentFileSystemLocation.Path);
		return string.IsNullOrWhiteSpace(repoPath) ? null : new Repository(repoPath);
	}

	private string ConstructPrompt(Repository? repository, RepositoryStatus? status)
	{
		var prompt = new StringBuilder();

		WriteWorkingDirectoryToShell(prompt);
		WriteOpeningToken(prompt);
		WriteCwd(prompt);

		var currentBackground = WriteAwsProfile(prompt);

		if (repository is null)
		{
			WriteClosingToken(prompt, currentBackground, DefaultColor);
			prompt.Append("\x1b[0m");
			return prompt.ToString();
		}

		var statusColor = GetGitStatusColor(repository, status);
		WriteClosingToken(prompt, currentBackground, statusColor);
		WriteGitBranch(prompt, statusColor, repository);
		WriteClosingToken(prompt, statusColor, DefaultColor);

		prompt.Append("\x1b[0m");
		return prompt.ToString();
	}

	private void WriteWorkingDirectoryToShell(StringBuilder prompt)
	{
		prompt.Append($"\x1b]9;9;{SessionState.Path.CurrentFileSystemLocation.Path}\x1b\\");
	}

	private void WriteOpeningToken(StringBuilder prompt)
	{
		prompt.Append(CwdBackground, DefaultColor, "\ue0b6");
	}

	private void WriteCwd(StringBuilder prompt)
	{
		var cwd = SessionState.Path.CurrentFileSystemLocation.Path;
		cwd = cwd.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~");
		prompt.Append(CwdForeground, CwdBackground, $"{cwd} ");
	}

	private AnsiColor WriteAwsProfile(StringBuilder prompt)
	{
		if (!SessionState.Path.CurrentFileSystemLocation.Path.StartsWith(@"C:\EDF\volvo"))
		{
			return CwdBackground;
		}

		if (SessionState.PSVariable.GetValue("ENV:AWS_PROFILE") is not string awsProfile)
		{
			return CwdBackground;
		}

		switch (awsProfile.ToUpperInvariant())
		{
			case "DEV":
				WriteClosingToken(prompt, CwdBackground, AwsProfileDevBackground);
				prompt.Append(AwsProfileForeground, AwsProfileDevBackground, "dev ");
				return AwsProfileDevBackground;
			case "QA":
				WriteClosingToken(prompt, CwdBackground, AwsProfileQaBackground);
				prompt.Append(AwsProfileForeground, AwsProfileQaBackground, "qa ");
				return AwsProfileQaBackground;
			case "PROD":
				WriteClosingToken(prompt, CwdBackground, AwsProfileProdBackground);
				prompt.Append(AwsProfileForeground, AwsProfileProdBackground, "prod ");
				return AwsProfileProdBackground;
			default:
				return CwdBackground;
		}
	}

	private void WriteClosingToken(StringBuilder prompt, AnsiColor foreground, AnsiColor background)
	{
		prompt.Append(foreground, background, "\ue0b0 ");
	}

	private AnsiColor GetGitStatusColor(Repository repository, RepositoryStatus? status) => (repository, status) switch
	{
		({ Head: { IsTracking: true, TrackingDetails: { AheadBy: not (null or 0) } or { BehindBy: not (null or 0) } } }, _) => GitOutOfSyncColor,
		(_, { IsDirty: false }) => GitCleanColor,
		(_, { IsDirty: true }) => GitDirtyColor,
		_ => GitUnknownColor,
	};

	private void WriteGitBranch(StringBuilder prompt, AnsiColor background, Repository repository)
	{
		var name = repository.Head.FriendlyName;
		if (name == "(no branch)")
		{
			name = repository.Head.Tip.Sha[..7];
		}
		prompt.Append(GitForeground, background, $"{name} ");
	}
}
