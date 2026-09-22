using System.CommandLine;
using GKit.Cli;
using GKit.Cli.Commands;

var manifest = FeatureManifest.Load();
var output = Console.Out;

Workspace CurrentWorkspace() => Workspace.Discover(Directory.GetCurrentDirectory());

// gkit new <template> <name> [-- passthrough]
var templateArgument = new Argument<string>("template")
{
  Description = $"One of: {string.Join(", ", NewCommand.Templates)}"
};
var nameArgument = new Argument<string>("name") { Description = "Name of the solution or project" };
var passThroughArgument = new Argument<string[]>("options")
{
  Description = "Options forwarded to dotnet new, e.g. --ui radzen --db npgsql --features quartz,reporting",
  Arity = ArgumentArity.ZeroOrMore
};

var newCommand = new Command("new", "Scaffold from the GKit templates.");
newCommand.Arguments.Add(templateArgument);
newCommand.Arguments.Add(nameArgument);
newCommand.Arguments.Add(passThroughArgument);
newCommand.TreatUnmatchedTokensAsErrors = false;
newCommand.SetAction(parseResult => NewCommand.Run(
  parseResult.GetValue(templateArgument)!,
  parseResult.GetValue(nameArgument)!,
  (parseResult.GetValue(passThroughArgument) ?? []).Concat(parseResult.UnmatchedTokens).ToList(),
  Directory.GetCurrentDirectory(),
  output,
  manifest));

// gkit link / unlink
var pathOption = new Option<string?>("--path") { Description = "Local GKit checkout. Auto-detected when omitted." };
var onlyOption = new Option<string?>("--only") { Description = "Comma separated package ids to link. Defaults to all GKit packages." };
var noSolutionOption = new Option<bool>("--no-solution") { Description = "Do not add or remove the linked projects in the solution file." };

var linkCommand = new Command("link", "Swap GKit package references for project references into a local checkout.");
linkCommand.Options.Add(pathOption);
linkCommand.Options.Add(onlyOption);
linkCommand.Options.Add(noSolutionOption);
linkCommand.SetAction(parseResult => LinkCommand.Run(
  CurrentWorkspace(),
  manifest,
  parseResult.GetValue(pathOption),
  (parseResult.GetValue(onlyOption) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
  !parseResult.GetValue(noSolutionOption),
  output));

var unlinkCommand = new Command("unlink", "Restore the package references replaced by 'gkit link'.");
unlinkCommand.Options.Add(noSolutionOption);
unlinkCommand.SetAction(parseResult => LinkCommand.Unlink(
  CurrentWorkspace(),
  !parseResult.GetValue(noSolutionOption),
  output));

// gkit doctor
var doctorCommand = new Command("doctor", "Report drift, retired packages and other rot in the solution.");
doctorCommand.SetAction(_ => DoctorCommand.Run(CurrentWorkspace(), manifest, output));

// gkit update
var toOption = new Option<string?>("--to") { Description = "Version to align on. Defaults to the highest already referenced." };
var dryRunOption = new Option<bool>("--dry-run") { Description = "Report what would change without writing." };

var updateCommand = new Command("update", "Align every GKit package version in the solution.");
updateCommand.Options.Add(toOption);
updateCommand.Options.Add(dryRunOption);
updateCommand.SetAction(parseResult => UpdateCommand.Run(
  CurrentWorkspace(), manifest, parseResult.GetValue(toOption), parseResult.GetValue(dryRunOption), output));

// gkit migrate ui
var applyOption = new Option<bool>("--apply") { Description = "Write the changes. Without it the command only reports." };

var migrateUiCommand = new Command("ui", "Apply the GKit.UI.* breaking renames and report what needs a human.");
migrateUiCommand.Options.Add(applyOption);
migrateUiCommand.SetAction(parseResult => MigrateUiCommand.Run(
  CurrentWorkspace(), manifest, parseResult.GetValue(applyOption), output));

var migrateCommand = new Command("migrate", "Codemods for GKit breaking changes.");
migrateCommand.Subcommands.Add(migrateUiCommand);

// gkit release
var projectsArgument = new Argument<string[]>("projects")
{
  Description = "Projects to publish. Defaults to every web or exe project in the solution.",
  Arity = ArgumentArity.ZeroOrMore
};
var ridOption = new Option<string>("--rid") { Description = "Runtime identifier", DefaultValueFactory = _ => "win-x64" };
var configurationOption = new Option<string>("--configuration", "-c") { DefaultValueFactory = _ => "Release" };
var frameworkOption = new Option<string>("--framework", "-f") { DefaultValueFactory = _ => "net10.0" };

var releaseCommand = new Command("release", "Publish and zip into build/ as <Project>.<height>-<branch>.zip.");
releaseCommand.Arguments.Add(projectsArgument);
releaseCommand.Options.Add(ridOption);
releaseCommand.Options.Add(configurationOption);
releaseCommand.Options.Add(frameworkOption);
releaseCommand.SetAction(parseResult => ReleaseCommand.Run(
  CurrentWorkspace(),
  parseResult.GetValue(projectsArgument) ?? [],
  parseResult.GetValue(ridOption)!,
  parseResult.GetValue(configurationOption)!,
  parseResult.GetValue(frameworkOption)!,
  output));

var root = new RootCommand("Command line companion to the GKit suite.");
root.Subcommands.Add(newCommand);
root.Subcommands.Add(linkCommand);
root.Subcommands.Add(unlinkCommand);
root.Subcommands.Add(doctorCommand);
root.Subcommands.Add(updateCommand);
root.Subcommands.Add(migrateCommand);
root.Subcommands.Add(releaseCommand);

return root.Parse(args).Invoke();
