namespace Nexplorer.App.Services;

/// <summary>
/// Static catalog of CLI tool definitions for intelligent command suggestions.
/// Add new tools here — they are automatically picked up by CliSuggestionService.
/// </summary>
public static class CliToolDefinitions
{
    public static readonly IReadOnlyList<CliToolDefinition> All = new[]
    {
        Git(), Dotnet(), Aws(), Az(), AzCopy(), Ssh(), Scp(),
        Docker(), Kubectl(), Npm(), Node(), Pip(), Python(),
        Terraform(), Cargo(), Go(),
    };

    // ═════════════════════════════════════════════════════════════════════════
    //  git
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition Git() => new("git", "Distributed version control system",
        Subcommands: new CliCommand[]
        {
            new("init",   "Create an empty Git repository"),
            new("clone",  "Clone a repository into a new directory", Flags: new CliFlag[]
            {
                new("--depth",    Description: "Shallow clone depth",       ExpectsValue: true),
                new("--branch",   "-b", "Branch to clone",                  ExpectsValue: true),
                new("--single-branch", Description: "Clone only one branch"),
                new("--recurse-submodules", Description: "Initialize submodules"),
                new("--bare",     Description: "Make a bare repository"),
            }),
            new("status", "Show the working tree status", Flags: new CliFlag[]
            {
                new("--short",  "-s", "Short format"),
                new("--branch", "-b", "Show branch info"),
                new("--porcelain", Description: "Machine-readable output"),
            }),
            new("add", "Add file contents to the index", Flags: new CliFlag[]
            {
                new("--all",     "-A", "Add all changes"),
                new("--patch",   "-p", "Interactively stage hunks"),
                new("--dry-run", "-n", "Just show what would be added"),
                new("--force",   "-f", "Allow adding otherwise ignored files"),
            }),
            new("commit", "Record changes to the repository", Flags: new CliFlag[]
            {
                new("--message", "-m", "Commit message",         ExpectsValue: true),
                new("--all",     "-a", "Stage modified & deleted files"),
                new("--amend",   Description: "Amend the last commit"),
                new("--no-edit", Description: "Reuse the last commit message"),
                new("--signoff", "-s", "Add Signed-off-by trailer"),
                new("--fixup",   Description: "Fixup a commit",  ExpectsValue: true),
            }),
            new("push", "Update remote refs", Flags: new CliFlag[]
            {
                new("--force",       "-f", "Force push"),
                new("--force-with-lease", Description: "Safe force push"),
                new("--set-upstream", "-u", "Set upstream tracking"),
                new("--tags",         Description: "Push all tags"),
                new("--delete",       Description: "Delete remote branch"),
                new("--dry-run",      "-n", "Simulate push"),
            }),
            new("pull", "Fetch and integrate remote changes", Flags: new CliFlag[]
            {
                new("--rebase",  "-r", "Rebase instead of merge"),
                new("--no-rebase", Description: "Merge (default)"),
                new("--ff-only", Description: "Fast-forward only"),
                new("--autostash", Description: "Stash before, apply after"),
            }),
            new("fetch", "Download objects and refs from remote", Flags: new CliFlag[]
            {
                new("--all",   Description: "Fetch all remotes"),
                new("--prune", "-p", "Remove stale remote-tracking branches"),
                new("--tags",  Description: "Fetch all tags"),
                new("--depth", Description: "Deepen shallow clone", ExpectsValue: true),
            }),
            new("branch", "List, create, or delete branches", Flags: new CliFlag[]
            {
                new("--all",    "-a", "List local and remote branches"),
                new("--delete", "-d", "Delete a branch"),
                new("--force",  "-D", "Force delete a branch"),
                new("--move",   "-m", "Rename a branch"),
                new("--remote", "-r", "List remote-tracking branches"),
            }),
            new("checkout", "Switch branches or restore working tree files", Flags: new CliFlag[]
            {
                new("--branch",  "-b", "Create and checkout a new branch"),
                new("--force",   "-f", "Force checkout"),
                new("--track",   "-t", "Set up upstream tracking"),
                new("--orphan",  Description: "Create orphan branch"),
            }),
            new("switch", "Switch branches", Flags: new CliFlag[]
            {
                new("--create",        "-c", "Create and switch to a new branch"),
                new("--force-create",  "-C", "Force create and switch"),
                new("--detach",        Description: "Detach HEAD"),
            }),
            new("merge", "Join two or more branches", Flags: new CliFlag[]
            {
                new("--no-ff",     Description: "Always create a merge commit"),
                new("--squash",    Description: "Squash commits"),
                new("--abort",     Description: "Abort the in-progress merge"),
                new("--continue",  Description: "Continue after resolving conflicts"),
                new("--strategy",  "-s", "Merge strategy",    ExpectsValue: true),
            }),
            new("rebase", "Reapply commits on top of another base", Flags: new CliFlag[]
            {
                new("--interactive", "-i", "Interactive rebase"),
                new("--onto",        Description: "Rebase onto a branch", ExpectsValue: true),
                new("--abort",       Description: "Abort rebase"),
                new("--continue",    Description: "Continue after resolving"),
                new("--skip",        Description: "Skip current patch"),
                new("--autosquash",  Description: "Automatically squash fixups"),
            }),
            new("log", "Show commit logs", Flags: new CliFlag[]
            {
                new("--oneline",  Description: "One line per commit"),
                new("--graph",    Description: "Draw a text-based graph"),
                new("--all",      Description: "All refs"),
                new("--stat",     Description: "Show file change stats"),
                new("--author",   Description: "Filter by author",      ExpectsValue: true),
                new("--since",    Description: "Show commits since date", ExpectsValue: true),
                new("--until",    Description: "Show commits until date", ExpectsValue: true),
                new("-n",         Description: "Limit number of commits", ExpectsValue: true),
                new("--format",   Description: "Pretty-print format",    ExpectsValue: true),
            }),
            new("diff", "Show changes between commits, tree, etc.", Flags: new CliFlag[]
            {
                new("--staged",   Description: "Show staged changes"),
                new("--cached",   Description: "Same as --staged"),
                new("--stat",     Description: "Show diffstat"),
                new("--name-only", Description: "Show only changed file names"),
                new("--no-color", Description: "Disable color output"),
            }),
            new("stash", "Stash the changes", Subcommands: new CliCommand[]
            {
                new("push",  "Save changes to stash", Flags: new CliFlag[]
                {
                    new("--message", "-m", "Stash message", ExpectsValue: true),
                    new("--include-untracked", "-u", "Include untracked files"),
                    new("--keep-index", Description: "Keep staged changes"),
                }),
                new("pop",   "Apply and remove stash"),
                new("apply", "Apply stash without removing"),
                new("list",  "List stash entries"),
                new("drop",  "Remove a stash entry"),
                new("clear", "Remove all stash entries"),
                new("show",  "Show stash contents"),
            }),
            new("reset", "Reset current HEAD to a specified state", Flags: new CliFlag[]
            {
                new("--soft",  Description: "Keep changes staged"),
                new("--mixed", Description: "Keep changes unstaged (default)"),
                new("--hard",  Description: "Discard all changes"),
            }),
            new("remote", "Manage tracked repositories", Subcommands: new CliCommand[]
            {
                new("add",     "Add a new remote"),
                new("remove",  "Remove a remote"),
                new("rename",  "Rename a remote"),
                new("show",    "Show remote details"),
                new("prune",   "Remove stale remote branches"),
                new("set-url", "Change remote URL"),
            }, Flags: new CliFlag[]
            {
                new("--verbose", "-v", "Show remote URLs"),
            }),
            new("tag", "Create, list, delete, or verify tags", Flags: new CliFlag[]
            {
                new("--annotate", "-a", "Create annotated tag"),
                new("--message",  "-m", "Tag message",       ExpectsValue: true),
                new("--delete",   "-d", "Delete a tag"),
                new("--list",     "-l", "List tags"),
                new("--force",    "-f", "Replace an existing tag"),
            }),
            new("cherry-pick", "Apply specific commits", Flags: new CliFlag[]
            {
                new("--no-commit", "-n", "Apply without committing"),
                new("--abort",     Description: "Abort cherry-pick"),
                new("--continue",  Description: "Continue after resolving"),
            }),
            new("clean", "Remove untracked files", Flags: new CliFlag[]
            {
                new("--force",       "-f", "Force cleaning"),
                new("--dry-run",     "-n", "Just show what would be removed"),
                new("--directories", "-d", "Also remove untracked directories"),
            }),
            new("bisect", "Find the commit that introduced a bug", Subcommands: new CliCommand[]
            {
                new("start", "Start bisecting"),
                new("bad",   "Mark current commit as bad"),
                new("good",  "Mark current commit as good"),
                new("reset", "End bisect session"),
                new("skip",  "Skip current commit"),
                new("log",   "Show bisect log"),
            }),
            new("worktree", "Manage multiple working trees", Subcommands: new CliCommand[]
            {
                new("add",    "Create a new working tree"),
                new("list",   "List working trees"),
                new("remove", "Remove a working tree"),
                new("prune",  "Prune stale working tree info"),
            }),
            new("submodule", "Manage submodules", Subcommands: new CliCommand[]
            {
                new("add",    "Add a submodule"),
                new("init",   "Initialize submodules"),
                new("update", "Update submodules", Flags: new CliFlag[]
                {
                    new("--init",      Description: "Initialize if needed"),
                    new("--recursive", Description: "Update recursively"),
                    new("--remote",    Description: "Use remote tracking branch"),
                }),
                new("status", "Show submodule status"),
                new("foreach", "Run command in each submodule"),
            }),
            new("config", "Get and set repository or global options", Flags: new CliFlag[]
            {
                new("--global", Description: "Use global config"),
                new("--local",  Description: "Use local config"),
                new("--list",   "-l", "List all"),
                new("--unset",  Description: "Remove a variable"),
            }),
            new("show",     "Show various types of objects"),
            new("blame",    "Show what revision and author last modified each line"),
            new("reflog",   "Manage reflog information"),
            new("restore",  "Restore working tree files", Flags: new CliFlag[]
            {
                new("--staged",  "-S", "Unstage files"),
                new("--source",  "-s", "Restore from a commit", ExpectsValue: true),
            }),
        },
        GlobalFlags: new CliFlag[]
        {
            new("--version",   Description: "Print version"),
            new("--help",      Description: "Show help"),
            new("--no-pager",  Description: "Do not pipe output into a pager"),
            new("--verbose",   "-v", "Be verbose"),
        });

    // ═════════════════════════════════════════════════════════════════════════
    //  dotnet
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition Dotnet() => new("dotnet", ".NET CLI",
        Subcommands: new CliCommand[]
        {
            new("new",     "Create a new project or file", Flags: new CliFlag[]
            {
                new("--name",     "-n", "Project name",     ExpectsValue: true),
                new("--output",   "-o", "Output directory",  ExpectsValue: true),
                new("--framework", "-f", "Target framework", ExpectsValue: true),
                new("--language",  Description: "Language (C#, F#, VB)", ExpectsValue: true),
                new("--list",      Description: "List available templates"),
                new("--force",     Description: "Force overwrite"),
            }),
            new("build",   "Build a .NET project", Flags: new CliFlag[]
            {
                new("--configuration", "-c", "Build configuration",     ExpectsValue: true),
                new("--output",        "-o", "Output directory",         ExpectsValue: true),
                new("--framework",     "-f", "Target framework",        ExpectsValue: true),
                new("--runtime",       "-r", "Target runtime",          ExpectsValue: true),
                new("--no-restore",    Description: "Skip NuGet restore"),
                new("--no-incremental", Description: "Clean build"),
                new("--verbosity",     "-v", "Verbosity level",         ExpectsValue: true),
            }),
            new("run",     "Run a .NET project", Flags: new CliFlag[]
            {
                new("--project",       "-p", "Project to run",         ExpectsValue: true),
                new("--configuration", "-c", "Configuration",          ExpectsValue: true),
                new("--framework",     "-f", "Target framework",       ExpectsValue: true),
                new("--launch-profile", Description: "Launch profile", ExpectsValue: true),
                new("--no-build",      Description: "Skip building"),
                new("--no-restore",    Description: "Skip restore"),
            }),
            new("test",    "Run unit tests", Flags: new CliFlag[]
            {
                new("--configuration", "-c", "Configuration",              ExpectsValue: true),
                new("--filter",        Description: "Test filter expression", ExpectsValue: true),
                new("--no-build",      Description: "Skip building"),
                new("--no-restore",    Description: "Skip restore"),
                new("--verbosity",     "-v", "Verbosity level",            ExpectsValue: true),
                new("--logger",        "-l", "Test logger",                ExpectsValue: true),
                new("--collect",       Description: "Data collector",      ExpectsValue: true),
                new("--blame",         Description: "Run tests in blame mode"),
            }),
            new("publish", "Publish a .NET project for deployment", Flags: new CliFlag[]
            {
                new("--configuration", "-c", "Configuration",       ExpectsValue: true),
                new("--output",        "-o", "Output directory",     ExpectsValue: true),
                new("--framework",     "-f", "Target framework",    ExpectsValue: true),
                new("--runtime",       "-r", "Target runtime",      ExpectsValue: true),
                new("--self-contained", Description: "Self-contained deployment"),
                new("--no-build",       Description: "Skip building"),
                new("--no-restore",     Description: "Skip restore"),
            }),
            new("restore", "Restore NuGet packages", Flags: new CliFlag[]
            {
                new("--source",    "-s", "Package source",     ExpectsValue: true),
                new("--packages",  Description: "Packages dir", ExpectsValue: true),
                new("--no-cache",  Description: "Skip HTTP cache"),
                new("--force",     Description: "Force all dependencies to be resolved"),
            }),
            new("clean",   "Clean build outputs", Flags: new CliFlag[]
            {
                new("--configuration", "-c", "Configuration",    ExpectsValue: true),
                new("--framework",     "-f", "Target framework", ExpectsValue: true),
                new("--output",        "-o", "Output directory",  ExpectsValue: true),
            }),
            new("pack",    "Create a NuGet package", Flags: new CliFlag[]
            {
                new("--configuration", "-c", "Configuration",    ExpectsValue: true),
                new("--output",        "-o", "Output directory",  ExpectsValue: true),
                new("--no-build",      Description: "Skip building"),
                new("--include-symbols", Description: "Include symbol packages"),
                new("--include-source",  Description: "Include source files"),
            }),
            new("add", "Add a package or reference", Subcommands: new CliCommand[]
            {
                new("package",   "Add a NuGet package reference", Flags: new CliFlag[]
                {
                    new("--version",   "-v", "Package version",       ExpectsValue: true),
                    new("--source",    "-s", "Package source",        ExpectsValue: true),
                    new("--prerelease", Description: "Allow prerelease"),
                }),
                new("reference", "Add a project-to-project reference"),
            }),
            new("remove", "Remove a package or reference", Subcommands: new CliCommand[]
            {
                new("package",   "Remove a NuGet package"),
                new("reference", "Remove a project reference"),
            }),
            new("list", "List project references or packages", Subcommands: new CliCommand[]
            {
                new("package",   "List NuGet packages", Flags: new CliFlag[]
                {
                    new("--outdated",        Description: "Show outdated packages"),
                    new("--include-transitive", Description: "Include transitive packages"),
                    new("--vulnerable",       Description: "Show vulnerable packages"),
                }),
                new("reference", "List project references"),
            }),
            new("sln", "Manage solution files", Subcommands: new CliCommand[]
            {
                new("add",    "Add project to solution"),
                new("remove", "Remove project from solution"),
                new("list",   "List projects in solution"),
            }),
            new("watch",   "File watcher that reruns commands", Flags: new CliFlag[]
            {
                new("--project", Description: "Project to watch", ExpectsValue: true),
                new("--no-hot-reload", Description: "Disable hot reload"),
            }),
            new("tool", "Manage .NET tools", Subcommands: new CliCommand[]
            {
                new("install",   "Install a .NET tool"),
                new("uninstall", "Uninstall a .NET tool"),
                new("update",    "Update a .NET tool"),
                new("list",      "List installed .NET tools"),
                new("restore",   "Restore local .NET tools"),
            }),
            new("nuget", "NuGet commands", Subcommands: new CliCommand[]
            {
                new("push",    "Push a package to a source"),
                new("delete",  "Delete a package from a source"),
                new("locals",  "Manage local NuGet resources"),
            }),
            new("format",  "Format code in a project", Flags: new CliFlag[]
            {
                new("--verify-no-changes", Description: "Verify already formatted"),
                new("--include",           Description: "Files to include", ExpectsValue: true),
                new("--exclude",           Description: "Files to exclude", ExpectsValue: true),
            }),
            new("ef", "Entity Framework Core tools", Subcommands: new CliCommand[]
            {
                new("migrations", "Manage EF migrations", Subcommands: new CliCommand[]
                {
                    new("add",    "Add a new migration"),
                    new("remove", "Remove the last migration"),
                    new("list",   "List available migrations"),
                    new("script", "Generate a SQL script"),
                }),
                new("database", "Database commands", Subcommands: new CliCommand[]
                {
                    new("update", "Update database to latest migration"),
                    new("drop",   "Drop the database"),
                }),
            }),
        },
        GlobalFlags: new CliFlag[]
        {
            new("--help",      "-h", "Show help"),
            new("--version",   Description: "Display .NET version"),
            new("--info",      Description: "Display .NET info"),
        });

    // ═════════════════════════════════════════════════════════════════════════
    //  aws
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition Aws() => new("aws", "Amazon Web Services CLI",
        Subcommands: new CliCommand[]
        {
            new("s3", "Amazon S3 commands", Subcommands: new CliCommand[]
            {
                new("ls",   "List S3 objects"),
                new("cp",   "Copy files to/from S3", Flags: new CliFlag[]
                {
                    new("--recursive",  Description: "Recursive copy"),
                    new("--exclude",    Description: "Exclude pattern",   ExpectsValue: true),
                    new("--include",    Description: "Include pattern",   ExpectsValue: true),
                    new("--dryrun",     Description: "Dry run"),
                    new("--acl",        Description: "ACL policy",       ExpectsValue: true),
                }),
                new("mv",   "Move files in S3"),
                new("rm",   "Remove S3 objects", Flags: new CliFlag[]
                {
                    new("--recursive",  Description: "Recursive delete"),
                    new("--dryrun",     Description: "Dry run"),
                }),
                new("mb",   "Create an S3 bucket"),
                new("rb",   "Remove an S3 bucket"),
                new("sync", "Sync directories with S3", Flags: new CliFlag[]
                {
                    new("--delete",     Description: "Delete files not in source"),
                    new("--exclude",    Description: "Exclude pattern",  ExpectsValue: true),
                    new("--include",    Description: "Include pattern",  ExpectsValue: true),
                    new("--dryrun",     Description: "Dry run"),
                }),
                new("presign", "Generate a pre-signed URL"),
            }),
            new("ec2", "Amazon EC2 commands", Subcommands: new CliCommand[]
            {
                new("describe-instances",      "List EC2 instances"),
                new("start-instances",         "Start EC2 instances"),
                new("stop-instances",          "Stop EC2 instances"),
                new("terminate-instances",     "Terminate EC2 instances"),
                new("run-instances",           "Launch EC2 instances"),
                new("describe-security-groups", "List security groups"),
                new("create-security-group",    "Create a security group"),
                new("describe-vpcs",            "List VPCs"),
                new("describe-subnets",         "List subnets"),
            }),
            new("iam", "AWS IAM commands", Subcommands: new CliCommand[]
            {
                new("list-users",        "List IAM users"),
                new("create-user",       "Create an IAM user"),
                new("delete-user",       "Delete an IAM user"),
                new("list-roles",        "List IAM roles"),
                new("create-role",       "Create an IAM role"),
                new("attach-role-policy", "Attach a policy to a role"),
                new("list-policies",      "List IAM policies"),
            }),
            new("lambda", "AWS Lambda commands", Subcommands: new CliCommand[]
            {
                new("list-functions",  "List Lambda functions"),
                new("invoke",          "Invoke a Lambda function"),
                new("create-function", "Create a Lambda function"),
                new("update-function-code", "Update function code"),
                new("delete-function", "Delete a Lambda function"),
                new("get-function",    "Get function details"),
            }),
            new("ecs", "Amazon ECS commands", Subcommands: new CliCommand[]
            {
                new("list-clusters",  "List ECS clusters"),
                new("list-services",  "List ECS services"),
                new("list-tasks",     "List ECS tasks"),
                new("describe-services", "Describe ECS services"),
                new("update-service", "Update an ECS service"),
            }),
            new("eks", "Amazon EKS commands", Subcommands: new CliCommand[]
            {
                new("list-clusters",     "List EKS clusters"),
                new("describe-cluster",  "Describe an EKS cluster"),
                new("update-kubeconfig", "Update kubeconfig"),
                new("create-cluster",    "Create an EKS cluster"),
            }),
            new("cloudformation", "AWS CloudFormation", Subcommands: new CliCommand[]
            {
                new("deploy",          "Deploy a stack"),
                new("describe-stacks", "Describe stacks"),
                new("list-stacks",     "List stacks"),
                new("delete-stack",    "Delete a stack"),
                new("create-stack",    "Create a stack"),
            }),
            new("sts", "AWS STS commands", Subcommands: new CliCommand[]
            {
                new("get-caller-identity", "Get current caller identity"),
                new("assume-role",         "Assume an IAM role"),
            }),
            new("ssm", "AWS Systems Manager", Subcommands: new CliCommand[]
            {
                new("get-parameter",        "Get an SSM parameter"),
                new("put-parameter",        "Create/update an SSM parameter"),
                new("get-parameters-by-path", "Get parameters by path"),
                new("start-session",        "Start an SSM session"),
            }),
            new("logs", "Amazon CloudWatch Logs", Subcommands: new CliCommand[]
            {
                new("describe-log-groups",  "List log groups"),
                new("describe-log-streams", "List log streams"),
                new("get-log-events",       "Get log events"),
                new("tail",                 "Tail log group"),
            }),
            new("sqs", "Amazon SQS commands", Subcommands: new CliCommand[]
            {
                new("send-message",    "Send a message"),
                new("receive-message", "Receive messages"),
                new("list-queues",     "List queues"),
                new("create-queue",    "Create a queue"),
            }),
            new("sns", "Amazon SNS commands", Subcommands: new CliCommand[]
            {
                new("publish",          "Publish a message"),
                new("list-topics",      "List topics"),
                new("create-topic",     "Create a topic"),
                new("list-subscriptions", "List subscriptions"),
            }),
            new("dynamodb", "Amazon DynamoDB commands", Subcommands: new CliCommand[]
            {
                new("list-tables",    "List tables"),
                new("describe-table", "Describe a table"),
                new("scan",           "Scan a table"),
                new("query",          "Query a table"),
                new("put-item",       "Put an item"),
                new("get-item",       "Get an item"),
            }),
            new("rds", "Amazon RDS commands", Subcommands: new CliCommand[]
            {
                new("describe-db-instances", "List DB instances"),
                new("create-db-instance",    "Create a DB instance"),
                new("delete-db-instance",    "Delete a DB instance"),
            }),
            new("ecr", "Amazon ECR commands", Subcommands: new CliCommand[]
            {
                new("get-login-password",  "Get Docker login password"),
                new("describe-repositories", "List repositories"),
                new("list-images",          "List images in a repository"),
            }),
            new("configure", "Configure AWS CLI", Subcommands: new CliCommand[]
            {
                new("set",  "Set a config value"),
                new("get",  "Get a config value"),
                new("list", "List config settings"),
                new("sso",  "Configure SSO"),
            }),
            new("sso", "AWS SSO commands", Subcommands: new CliCommand[]
            {
                new("login",  "Log in via SSO"),
                new("logout", "Log out from SSO"),
            }),
        },
        GlobalFlags: new CliFlag[]
        {
            new("--region",   Description: "AWS region",               ExpectsValue: true),
            new("--profile",  Description: "Named profile",            ExpectsValue: true),
            new("--output",   Description: "Output format (json/text/table)", ExpectsValue: true),
            new("--query",    Description: "JMESPath query",           ExpectsValue: true),
            new("--no-paginate", Description: "Disable pagination"),
            new("--dry-run",  Description: "Dry run"),
        });

    // ═════════════════════════════════════════════════════════════════════════
    //  az (Azure CLI)
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition Az() => new("az", "Azure CLI",
        Subcommands: new CliCommand[]
        {
            new("login",   "Log in to Azure", Flags: new CliFlag[]
            {
                new("--use-device-code", Description: "Device code flow"),
                new("--tenant",          "-t", "Tenant ID",     ExpectsValue: true),
                new("--service-principal", Description: "Service principal login"),
            }),
            new("logout",  "Log out from Azure"),
            new("account", "Manage Azure subscriptions", Subcommands: new CliCommand[]
            {
                new("show",  "Show current subscription"),
                new("list",  "List all subscriptions"),
                new("set",   "Set the active subscription", Flags: new CliFlag[]
                {
                    new("--subscription", "-s", "Subscription ID/name", ExpectsValue: true),
                }),
            }),
            new("group", "Manage resource groups", Subcommands: new CliCommand[]
            {
                new("create", "Create a resource group", Flags: new CliFlag[]
                {
                    new("--name",     "-n", "Resource group name", ExpectsValue: true),
                    new("--location", "-l", "Location",           ExpectsValue: true),
                }),
                new("delete", "Delete a resource group"),
                new("list",   "List resource groups"),
                new("show",   "Show resource group details"),
            }),
            new("vm", "Manage virtual machines", Subcommands: new CliCommand[]
            {
                new("create",  "Create a virtual machine"),
                new("delete",  "Delete a virtual machine"),
                new("list",    "List virtual machines"),
                new("show",    "Show VM details"),
                new("start",   "Start a VM"),
                new("stop",    "Stop a VM"),
                new("restart", "Restart a VM"),
                new("deallocate", "Deallocate a VM"),
            }),
            new("storage", "Manage Azure storage", Subcommands: new CliCommand[]
            {
                new("account", "Manage storage accounts", Subcommands: new CliCommand[]
                {
                    new("create", "Create a storage account"),
                    new("delete", "Delete a storage account"),
                    new("list",   "List storage accounts"),
                    new("show",   "Show storage account details"),
                    new("keys",   "Manage storage account keys", Subcommands: new CliCommand[]
                    {
                        new("list",  "List storage account keys"),
                        new("renew", "Renew a storage account key"),
                    }),
                }),
                new("blob", "Manage blob storage", Subcommands: new CliCommand[]
                {
                    new("upload",   "Upload a blob"),
                    new("download", "Download a blob"),
                    new("list",     "List blobs"),
                    new("delete",   "Delete a blob"),
                    new("copy",     "Copy a blob"),
                }),
                new("container", "Manage blob containers", Subcommands: new CliCommand[]
                {
                    new("create", "Create a container"),
                    new("delete", "Delete a container"),
                    new("list",   "List containers"),
                }),
            }),
            new("webapp", "Manage web apps", Subcommands: new CliCommand[]
            {
                new("create",   "Create a web app"),
                new("delete",   "Delete a web app"),
                new("list",     "List web apps"),
                new("show",     "Show web app details"),
                new("start",    "Start a web app"),
                new("stop",     "Stop a web app"),
                new("restart",  "Restart a web app"),
                new("deploy",   "Deploy to a web app"),
                new("browse",   "Open web app in browser"),
            }),
            new("functionapp", "Manage function apps", Subcommands: new CliCommand[]
            {
                new("create",  "Create a function app"),
                new("delete",  "Delete a function app"),
                new("list",    "List function apps"),
                new("show",    "Show function app details"),
                new("deploy",  "Deploy function app"),
            }),
            new("aks", "Manage Azure Kubernetes Service", Subcommands: new CliCommand[]
            {
                new("create",          "Create an AKS cluster"),
                new("delete",          "Delete an AKS cluster"),
                new("list",            "List AKS clusters"),
                new("show",            "Show AKS cluster details"),
                new("get-credentials", "Get cluster credentials"),
                new("scale",           "Scale a node pool"),
                new("upgrade",         "Upgrade an AKS cluster"),
            }),
            new("acr", "Manage Azure Container Registry", Subcommands: new CliCommand[]
            {
                new("create",  "Create a container registry"),
                new("login",   "Log in to a registry"),
                new("list",    "List registries"),
                new("build",   "Build an image"),
                new("push",    "Push an image"),
                new("repository", "Manage repositories", Subcommands: new CliCommand[]
                {
                    new("list",        "List repositories"),
                    new("show-tags",   "Show tags"),
                    new("delete",      "Delete a repository"),
                }),
            }),
            new("keyvault", "Manage Azure Key Vault", Subcommands: new CliCommand[]
            {
                new("create",  "Create a key vault"),
                new("delete",  "Delete a key vault"),
                new("list",    "List key vaults"),
                new("secret",  "Manage secrets", Subcommands: new CliCommand[]
                {
                    new("set",    "Set a secret"),
                    new("show",   "Show a secret"),
                    new("list",   "List secrets"),
                    new("delete", "Delete a secret"),
                }),
            }),
            new("sql", "Manage Azure SQL", Subcommands: new CliCommand[]
            {
                new("server", "Manage SQL servers", Subcommands: new CliCommand[]
                {
                    new("create", "Create a SQL server"),
                    new("delete", "Delete a SQL server"),
                    new("list",   "List SQL servers"),
                }),
                new("db", "Manage SQL databases", Subcommands: new CliCommand[]
                {
                    new("create", "Create a database"),
                    new("delete", "Delete a database"),
                    new("list",   "List databases"),
                    new("show",   "Show database details"),
                }),
            }),
            new("network", "Manage Azure networking", Subcommands: new CliCommand[]
            {
                new("vnet", "Manage virtual networks", Subcommands: new CliCommand[]
                {
                    new("create", "Create a VNet"),
                    new("delete", "Delete a VNet"),
                    new("list",   "List VNets"),
                }),
                new("nsg", "Manage network security groups", Subcommands: new CliCommand[]
                {
                    new("create", "Create an NSG"),
                    new("list",   "List NSGs"),
                    new("rule",   "Manage NSG rules", Subcommands: new CliCommand[]
                    {
                        new("create", "Create an NSG rule"),
                        new("list",   "List NSG rules"),
                        new("delete", "Delete an NSG rule"),
                    }),
                }),
            }),
            new("monitor", "Manage Azure Monitor", Subcommands: new CliCommand[]
            {
                new("log-analytics", "Manage Log Analytics", Subcommands: new CliCommand[]
                {
                    new("workspace", "Manage workspaces", Subcommands: new CliCommand[]
                    {
                        new("create", "Create a workspace"),
                        new("list",   "List workspaces"),
                        new("show",   "Show workspace details"),
                    }),
                }),
                new("app-insights", "Manage Application Insights"),
            }),
            new("ad", "Manage Azure Active Directory", Subcommands: new CliCommand[]
            {
                new("user",  "Manage AD users"),
                new("group", "Manage AD groups"),
                new("sp",    "Manage service principals", Subcommands: new CliCommand[]
                {
                    new("create",       "Create a service principal"),
                    new("list",         "List service principals"),
                    new("show",         "Show a service principal"),
                    new("credential",   "Manage credentials"),
                }),
                new("app",   "Manage app registrations"),
            }),
        },
        GlobalFlags: new CliFlag[]
        {
            new("--output",         "-o", "Output format (json/table/tsv/yaml)", ExpectsValue: true),
            new("--query",          Description: "JMESPath query",               ExpectsValue: true),
            new("--subscription",   Description: "Subscription ID/name",         ExpectsValue: true),
            new("--resource-group", "-g", "Resource group name",                 ExpectsValue: true),
            new("--verbose",        Description: "Increase verbosity"),
            new("--debug",          Description: "Show debug output"),
            new("--help",           "-h", "Show help"),
        });

    // ═════════════════════════════════════════════════════════════════════════
    //  azcopy
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition AzCopy() => new("azcopy", "Azure storage data transfer tool",
        Subcommands: new CliCommand[]
        {
            new("copy",   "Copy data to/from Azure", Flags: new CliFlag[]
            {
                new("--recursive",         Description: "Recursive copy"),
                new("--overwrite",         Description: "Overwrite behavior",       ExpectsValue: true),
                new("--include-pattern",   Description: "Include files matching",   ExpectsValue: true),
                new("--exclude-pattern",   Description: "Exclude files matching",   ExpectsValue: true),
                new("--block-size-mb",     Description: "Block size in MB",         ExpectsValue: true),
                new("--put-md5",           Description: "Create MD5 hash"),
                new("--dry-run",           Description: "Dry run"),
            }),
            new("sync",   "Synchronize source and destination", Flags: new CliFlag[]
            {
                new("--recursive",        Description: "Recursive sync"),
                new("--delete-destination", Description: "Delete extra files at dest", ExpectsValue: true),
                new("--include-pattern",  Description: "Include pattern",   ExpectsValue: true),
                new("--exclude-pattern",  Description: "Exclude pattern",   ExpectsValue: true),
            }),
            new("remove", "Remove blobs or files", Flags: new CliFlag[]
            {
                new("--recursive",       Description: "Recursive remove"),
                new("--include-pattern", Description: "Include pattern", ExpectsValue: true),
                new("--dry-run",         Description: "Dry run"),
            }),
            new("list",   "List entities in a container"),
            new("make",   "Create a container or file share"),
            new("login",  "Log in to Azure storage", Flags: new CliFlag[]
            {
                new("--identity",        Description: "Use managed identity"),
                new("--service-principal", Description: "Service principal login"),
                new("--tenant-id",       Description: "Azure AD tenant ID",  ExpectsValue: true),
            }),
            new("logout", "Log out"),
            new("jobs",   "Manage transfer jobs", Subcommands: new CliCommand[]
            {
                new("list",   "List transfer jobs"),
                new("show",   "Show job details"),
                new("resume", "Resume a job"),
                new("remove", "Remove a job"),
                new("clean",  "Clean job data"),
            }),
            new("env",    "Show environment variables"),
            new("bench",  "Run a performance benchmark"),
        },
        GlobalFlags: new CliFlag[]
        {
            new("--cap-mbps",    Description: "Cap transfer rate (Mbps)",  ExpectsValue: true),
            new("--log-level",   Description: "Log verbosity",             ExpectsValue: true),
            new("--output-type", Description: "Output format",             ExpectsValue: true),
        });

    // ═════════════════════════════════════════════════════════════════════════
    //  ssh / scp
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition Ssh() => new("ssh", "OpenSSH client",
        GlobalFlags: new CliFlag[]
        {
            new("-p", Description: "Port number",                   ExpectsValue: true),
            new("-i", Description: "Identity file (private key)",   ExpectsValue: true),
            new("-l", Description: "Login name",                    ExpectsValue: true),
            new("-o", Description: "SSH option",                    ExpectsValue: true),
            new("-L", Description: "Local port forwarding",         ExpectsValue: true),
            new("-R", Description: "Remote port forwarding",        ExpectsValue: true),
            new("-D", Description: "Dynamic port forwarding (SOCKS)", ExpectsValue: true),
            new("-N", Description: "No command execution (tunnel only)"),
            new("-f", Description: "Go to background before command execution"),
            new("-v", Description: "Verbose mode"),
            new("-X", Description: "Enable X11 forwarding"),
            new("-A", Description: "Enable agent forwarding"),
            new("-J", Description: "Jump host (ProxyJump)",         ExpectsValue: true),
            new("-t", Description: "Force pseudo-terminal allocation"),
            new("-q", Description: "Quiet mode"),
            new("-C", Description: "Enable compression"),
        });

    private static CliToolDefinition Scp() => new("scp", "Secure file copy over SSH",
        GlobalFlags: new CliFlag[]
        {
            new("-r", Description: "Recursive copy"),
            new("-P", Description: "Port number",                 ExpectsValue: true),
            new("-i", Description: "Identity file (private key)", ExpectsValue: true),
            new("-l", Description: "Limit bandwidth (Kbit/s)",    ExpectsValue: true),
            new("-C", Description: "Enable compression"),
            new("-v", Description: "Verbose mode"),
            new("-q", Description: "Quiet mode"),
            new("-o", Description: "SSH option",                  ExpectsValue: true),
        });

    // ═════════════════════════════════════════════════════════════════════════
    //  docker
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition Docker() => new("docker", "Container platform",
        Subcommands: new CliCommand[]
        {
            new("build",   "Build an image from a Dockerfile", Flags: new CliFlag[]
            {
                new("--tag",     "-t", "Name and tag (name:tag)",    ExpectsValue: true),
                new("--file",    "-f", "Dockerfile path",            ExpectsValue: true),
                new("--no-cache", Description: "Do not use cache"),
                new("--build-arg", Description: "Set build argument", ExpectsValue: true),
                new("--target",  Description: "Build stage target",   ExpectsValue: true),
                new("--platform", Description: "Target platform",     ExpectsValue: true),
            }),
            new("run",     "Run a command in a new container", Flags: new CliFlag[]
            {
                new("--detach",      "-d", "Run in background"),
                new("--interactive", "-i", "Keep STDIN open"),
                new("--tty",         "-t", "Allocate a pseudo-TTY"),
                new("--name",        Description: "Container name",     ExpectsValue: true),
                new("--publish",     "-p", "Publish port (host:ctr)",   ExpectsValue: true),
                new("--volume",      "-v", "Bind mount a volume",       ExpectsValue: true),
                new("--env",         "-e", "Set environment variable",  ExpectsValue: true),
                new("--rm",          Description: "Remove after exit"),
                new("--network",     Description: "Connect to network", ExpectsValue: true),
                new("--platform",    Description: "Target platform",    ExpectsValue: true),
                new("--restart",     Description: "Restart policy",     ExpectsValue: true),
            }),
            new("ps",      "List containers", Flags: new CliFlag[]
            {
                new("--all",    "-a", "Show all containers"),
                new("--quiet",  "-q", "Only show IDs"),
                new("--filter", "-f", "Filter by condition",   ExpectsValue: true),
            }),
            new("images",  "List images", Flags: new CliFlag[]
            {
                new("--all",    "-a", "Show all images"),
                new("--quiet",  "-q", "Only show IDs"),
                new("--filter", "-f", "Filter",   ExpectsValue: true),
            }),
            new("pull",    "Pull an image from a registry"),
            new("push",    "Push an image to a registry"),
            new("exec",    "Run a command in a running container", Flags: new CliFlag[]
            {
                new("--interactive", "-i", "Keep STDIN open"),
                new("--tty",         "-t", "Allocate a pseudo-TTY"),
                new("--detach",      "-d", "Run in background"),
                new("--env",         "-e", "Set environment variable", ExpectsValue: true),
            }),
            new("stop",    "Stop running containers"),
            new("start",   "Start stopped containers"),
            new("restart", "Restart containers"),
            new("rm",      "Remove containers", Flags: new CliFlag[]
            {
                new("--force",   "-f", "Force removal"),
                new("--volumes", "-v", "Remove volumes"),
            }),
            new("rmi",     "Remove images", Flags: new CliFlag[]
            {
                new("--force", "-f", "Force removal"),
            }),
            new("logs",    "View container logs", Flags: new CliFlag[]
            {
                new("--follow",    "-f", "Follow output"),
                new("--tail",      Description: "Number of lines", ExpectsValue: true),
                new("--timestamps", "-t", "Show timestamps"),
            }),
            new("inspect", "Return low-level info on Docker objects"),
            new("network", "Manage networks", Subcommands: new CliCommand[]
            {
                new("create",     "Create a network"),
                new("ls",         "List networks"),
                new("rm",         "Remove a network"),
                new("inspect",    "Inspect a network"),
                new("connect",    "Connect a container to a network"),
                new("disconnect", "Disconnect a container from a network"),
            }),
            new("volume",  "Manage volumes", Subcommands: new CliCommand[]
            {
                new("create",  "Create a volume"),
                new("ls",      "List volumes"),
                new("rm",      "Remove a volume"),
                new("inspect", "Inspect a volume"),
                new("prune",   "Remove unused volumes"),
            }),
            new("compose", "Docker Compose", Subcommands: new CliCommand[]
            {
                new("up",      "Create and start services", Flags: new CliFlag[]
                {
                    new("--detach",  "-d", "Detached mode"),
                    new("--build",   Description: "Build images before starting"),
                    new("--force-recreate", Description: "Recreate containers"),
                    new("--scale",   Description: "Scale service",   ExpectsValue: true),
                }),
                new("down",    "Stop and remove containers", Flags: new CliFlag[]
                {
                    new("--volumes",        "-v", "Remove volumes"),
                    new("--remove-orphans", Description: "Remove orphan containers"),
                }),
                new("build",   "Build or rebuild services"),
                new("ps",      "List containers"),
                new("logs",    "View output from containers"),
                new("pull",    "Pull service images"),
                new("restart", "Restart services"),
                new("stop",    "Stop services"),
                new("exec",    "Execute a command in a running service container"),
                new("config",  "Validate and view the Compose file"),
            }),
            new("system",  "Manage Docker", Subcommands: new CliCommand[]
            {
                new("prune", "Remove unused data", Flags: new CliFlag[]
                {
                    new("--all",    "-a", "Remove all unused images"),
                    new("--force",  "-f", "No confirmation prompt"),
                    new("--volumes", Description: "Prune volumes too"),
                }),
                new("df",    "Show Docker disk usage"),
                new("info",  "Display system-wide information"),
            }),
            new("tag",     "Create a tag for an image"),
            new("login",   "Log in to a registry"),
            new("logout",  "Log out from a registry"),
        },
        GlobalFlags: new CliFlag[]
        {
            new("--help",    Description: "Show help"),
            new("--version", Description: "Print version"),
        });

    // ═════════════════════════════════════════════════════════════════════════
    //  kubectl
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition Kubectl() => new("kubectl", "Kubernetes CLI",
        Subcommands: new CliCommand[]
        {
            new("get",       "Display resources", Subcommands: new CliCommand[]
            {
                new("pods",         "List pods"),
                new("services",     "List services"),
                new("deployments",  "List deployments"),
                new("nodes",        "List nodes"),
                new("namespaces",   "List namespaces"),
                new("configmaps",   "List configmaps"),
                new("secrets",      "List secrets"),
                new("ingress",      "List ingresses"),
                new("events",       "List events"),
                new("pvc",          "List persistent volume claims"),
                new("all",          "List all resources"),
            }, Flags: new CliFlag[]
            {
                new("--output",       "-o", "Output format",        ExpectsValue: true),
                new("--namespace",    "-n", "Namespace",            ExpectsValue: true),
                new("--all-namespaces", "-A", "All namespaces"),
                new("--selector",     "-l", "Label selector",       ExpectsValue: true),
                new("--watch",        "-w", "Watch for changes"),
            }),
            new("describe", "Describe a resource"),
            new("apply",    "Apply a configuration", Flags: new CliFlag[]
            {
                new("--filename",  "-f", "File or directory", ExpectsValue: true),
                new("--kustomize", "-k", "Kustomize dir",     ExpectsValue: true),
                new("--dry-run",   Description: "Dry run mode",  ExpectsValue: true),
            }),
            new("delete",   "Delete resources", Flags: new CliFlag[]
            {
                new("--filename",  "-f", "File or directory", ExpectsValue: true),
                new("--all",       Description: "Delete all in namespace"),
                new("--force",     Description: "Immediate deletion"),
                new("--grace-period", Description: "Grace period (seconds)", ExpectsValue: true),
            }),
            new("create",   "Create a resource", Flags: new CliFlag[]
            {
                new("--filename", "-f", "File", ExpectsValue: true),
            }),
            new("logs",     "Print pod logs", Flags: new CliFlag[]
            {
                new("--follow",    "-f", "Follow log output"),
                new("--tail",      Description: "Lines to show",      ExpectsValue: true),
                new("--container", "-c", "Container name",            ExpectsValue: true),
                new("--previous",  "-p", "Previous terminated container"),
                new("--timestamps", Description: "Include timestamps"),
            }),
            new("exec",     "Execute a command in a container", Flags: new CliFlag[]
            {
                new("--stdin", "-i", "Keep STDIN open"),
                new("--tty",   "-t", "Allocate TTY"),
                new("--container", "-c", "Container name", ExpectsValue: true),
            }),
            new("port-forward", "Forward local ports to a pod"),
            new("scale",    "Scale a deployment", Flags: new CliFlag[]
            {
                new("--replicas", Description: "Number of replicas", ExpectsValue: true),
            }),
            new("rollout",  "Manage rollouts", Subcommands: new CliCommand[]
            {
                new("status",  "Show rollout status"),
                new("history", "View rollout history"),
                new("undo",    "Undo a rollout"),
                new("restart", "Restart a resource"),
            }),
            new("config",   "Modify kubeconfig", Subcommands: new CliCommand[]
            {
                new("view",                "Display kubeconfig"),
                new("use-context",         "Set the current context"),
                new("get-contexts",        "List contexts"),
                new("current-context",     "Display current context"),
                new("set-context",         "Set context options"),
            }),
            new("top",      "Display resource usage", Subcommands: new CliCommand[]
            {
                new("node", "Display node resource usage"),
                new("pod",  "Display pod resource usage"),
            }),
            new("edit",     "Edit a resource"),
            new("label",    "Update labels on a resource"),
            new("annotate", "Update annotations on a resource"),
            new("cp",       "Copy files to/from containers"),
            new("drain",    "Drain a node for maintenance"),
            new("cordon",   "Mark node as unschedulable"),
            new("uncordon", "Mark node as schedulable"),
            new("taint",    "Update taints on a node"),
        },
        GlobalFlags: new CliFlag[]
        {
            new("--namespace",     "-n", "Namespace",            ExpectsValue: true),
            new("--context",       Description: "Kubeconfig context", ExpectsValue: true),
            new("--kubeconfig",    Description: "Kubeconfig file",    ExpectsValue: true),
            new("--output",        "-o", "Output format",        ExpectsValue: true),
            new("--help",          "-h", "Show help"),
        });

    // ═════════════════════════════════════════════════════════════════════════
    //  npm / node
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition Npm() => new("npm", "Node.js package manager",
        Subcommands: new CliCommand[]
        {
            new("install",   "Install packages", Flags: new CliFlag[]
            {
                new("--save-dev",  "-D", "Save as dev dependency"),
                new("--save-exact", "-E", "Save exact version"),
                new("--global",    "-g", "Install globally"),
                new("--legacy-peer-deps", Description: "Ignore peer dep conflicts"),
                new("--force",     Description: "Force installation"),
                new("--no-save",   Description: "Don't save to package.json"),
            }),
            new("uninstall", "Remove packages"),
            new("update",    "Update packages"),
            new("run",       "Run a package script"),
            new("start",     "Run the start script"),
            new("test",      "Run the test script"),
            new("build",     "Run the build script"),
            new("init",      "Create package.json", Flags: new CliFlag[]
            {
                new("--yes", "-y", "Accept defaults"),
            }),
            new("publish",   "Publish a package", Flags: new CliFlag[]
            {
                new("--tag",    Description: "Distribution tag",    ExpectsValue: true),
                new("--access", Description: "Access level (public/restricted)", ExpectsValue: true),
                new("--dry-run", Description: "Dry run"),
            }),
            new("pack",      "Create a tarball from a package"),
            new("link",      "Symlink a package locally"),
            new("ls",        "List installed packages", Flags: new CliFlag[]
            {
                new("--all",    "-a", "Show all installed packages"),
                new("--depth",  Description: "Dependency depth",   ExpectsValue: true),
                new("--global", "-g", "List global packages"),
                new("--json",   Description: "JSON output"),
            }),
            new("outdated",  "Check for outdated packages"),
            new("audit",     "Run a security audit", Flags: new CliFlag[]
            {
                new("--fix",  Description: "Auto-fix vulnerabilities"),
                new("--json", Description: "JSON output"),
            }),
            new("cache",     "Manage npm cache", Subcommands: new CliCommand[]
            {
                new("clean", "Remove cache data", Flags: new CliFlag[]
                {
                    new("--force", Description: "Force clean"),
                }),
                new("ls",    "List cache contents"),
                new("verify", "Verify cache integrity"),
            }),
            new("config",    "Manage npm config", Subcommands: new CliCommand[]
            {
                new("set",    "Set a config value"),
                new("get",    "Get a config value"),
                new("list",   "List all config"),
                new("delete", "Delete a config value"),
            }),
            new("exec",      "Run a package binary (npx)"),
            new("ci",        "Clean install from lockfile"),
            new("version",   "Bump a package version"),
            new("view",      "View package info"),
            new("search",    "Search for packages"),
            new("login",     "Log in to registry"),
            new("logout",    "Log out from registry"),
            new("whoami",    "Show current user"),
        });

    private static CliToolDefinition Node() => new("node", "Node.js runtime",
        GlobalFlags: new CliFlag[]
        {
            new("--version",     "-v", "Print Node.js version"),
            new("--eval",        "-e", "Evaluate script",              ExpectsValue: true),
            new("--print",       "-p", "Evaluate and print script",    ExpectsValue: true),
            new("--check",       "-c", "Syntax check only"),
            new("--inspect",     Description: "Enable inspector"),
            new("--inspect-brk", Description: "Enable inspector, break at start"),
            new("--require",     "-r", "Preload module",               ExpectsValue: true),
            new("--experimental-modules", Description: "Enable ES modules"),
            new("--max-old-space-size", Description: "Set V8 heap size (MB)", ExpectsValue: true),
        });

    // ═════════════════════════════════════════════════════════════════════════
    //  pip / python
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition Pip() => new("pip", "Python package manager",
        Subcommands: new CliCommand[]
        {
            new("install",   "Install packages", Flags: new CliFlag[]
            {
                new("--requirement",    "-r", "Requirements file",  ExpectsValue: true),
                new("--upgrade",        "-U", "Upgrade packages"),
                new("--user",           Description: "Install to user dir"),
                new("--no-deps",        Description: "Skip dependencies"),
                new("--force-reinstall", Description: "Force reinstall"),
                new("--pre",            Description: "Allow pre-release"),
                new("--editable",       "-e", "Install in dev mode",  ExpectsValue: true),
                new("--index-url",      "-i", "Custom PyPI URL",      ExpectsValue: true),
            }),
            new("uninstall", "Uninstall packages", Flags: new CliFlag[]
            {
                new("--yes", "-y", "Don't ask for confirmation"),
            }),
            new("freeze",    "Output installed packages in requirements format"),
            new("list",      "List installed packages", Flags: new CliFlag[]
            {
                new("--outdated", "-o", "Show outdated packages"),
                new("--format",   Description: "Output format",   ExpectsValue: true),
            }),
            new("show",      "Show information about installed packages"),
            new("search",    "Search PyPI"),
            new("download",  "Download packages"),
            new("check",     "Verify installed packages have compatible dependencies"),
            new("cache",     "Manage pip cache", Subcommands: new CliCommand[]
            {
                new("info",   "Show cache info"),
                new("list",   "List cache entries"),
                new("remove", "Remove cache entries"),
                new("purge",  "Purge all cache"),
            }),
            new("config",    "Manage pip configuration", Subcommands: new CliCommand[]
            {
                new("list",  "List config"),
                new("get",   "Get a config value"),
                new("set",   "Set a config value"),
                new("unset", "Unset a config value"),
            }),
        });

    private static CliToolDefinition Python() => new("python", "Python interpreter",
        GlobalFlags: new CliFlag[]
        {
            new("--version",   "-V", "Print version"),
            new("-c",          Description: "Execute code string",     ExpectsValue: true),
            new("-m",          Description: "Run module as script",    ExpectsValue: true),
            new("-i",          Description: "Inspect interactively after running"),
            new("-u",          Description: "Unbuffered stdout/stderr"),
            new("-v",          Description: "Verbose (trace imports)"),
            new("-W",          Description: "Warning control",         ExpectsValue: true),
            new("-O",          Description: "Optimize bytecode"),
            new("-B",          Description: "Don't write .pyc files"),
        });

    // ═════════════════════════════════════════════════════════════════════════
    //  terraform
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition Terraform() => new("terraform", "Infrastructure as Code tool",
        Subcommands: new CliCommand[]
        {
            new("init",     "Initialize a working directory", Flags: new CliFlag[]
            {
                new("-backend-config", Description: "Backend config",      ExpectsValue: true),
                new("-upgrade",        Description: "Upgrade modules/providers"),
                new("-reconfigure",    Description: "Reconfigure backend"),
                new("-migrate-state",  Description: "Migrate state"),
            }),
            new("plan",     "Create an execution plan", Flags: new CliFlag[]
            {
                new("-var",        Description: "Set a variable",          ExpectsValue: true),
                new("-var-file",   Description: "Variable file",           ExpectsValue: true),
                new("-out",        Description: "Write plan to file",      ExpectsValue: true),
                new("-target",     Description: "Target resource",         ExpectsValue: true),
                new("-destroy",    Description: "Plan destruction"),
                new("-refresh-only", Description: "Only refresh state"),
            }),
            new("apply",    "Apply changes", Flags: new CliFlag[]
            {
                new("-auto-approve", Description: "Skip interactive approval"),
                new("-var",          Description: "Set a variable",      ExpectsValue: true),
                new("-var-file",     Description: "Variable file",       ExpectsValue: true),
                new("-target",       Description: "Target resource",     ExpectsValue: true),
                new("-parallelism",  Description: "Number of operations", ExpectsValue: true),
            }),
            new("destroy",  "Destroy infrastructure", Flags: new CliFlag[]
            {
                new("-auto-approve", Description: "Skip approval"),
                new("-target",       Description: "Target resource", ExpectsValue: true),
            }),
            new("validate", "Validate the configuration"),
            new("fmt",      "Format configuration files", Flags: new CliFlag[]
            {
                new("-check", Description: "Check if already formatted"),
                new("-diff",  Description: "Show diff"),
                new("-recursive", Description: "Format recursively"),
            }),
            new("output",   "Show output values", Flags: new CliFlag[]
            {
                new("-json",  Description: "JSON format"),
                new("-raw",   Description: "Raw string output"),
            }),
            new("state",    "Advanced state management", Subcommands: new CliCommand[]
            {
                new("list",   "List resources in state"),
                new("show",   "Show a resource in state"),
                new("mv",     "Move a resource in state"),
                new("rm",     "Remove a resource from state"),
                new("pull",   "Pull remote state"),
                new("push",   "Push local state to remote"),
            }),
            new("import",   "Import existing infrastructure"),
            new("taint",    "Mark a resource for recreation"),
            new("untaint",  "Unmark a resource"),
            new("workspace", "Manage workspaces", Subcommands: new CliCommand[]
            {
                new("new",    "Create a workspace"),
                new("select", "Select a workspace"),
                new("list",   "List workspaces"),
                new("show",   "Show current workspace"),
                new("delete", "Delete a workspace"),
            }),
            new("graph",    "Create a visual graph of configuration"),
            new("providers", "Show required providers"),
            new("refresh",  "Update the state file"),
            new("show",     "Show a plan file"),
        },
        GlobalFlags: new CliFlag[]
        {
            new("-help",     Description: "Show help"),
            new("-version",  Description: "Show version"),
            new("-chdir",    Description: "Switch working directory", ExpectsValue: true),
        });

    // ═════════════════════════════════════════════════════════════════════════
    //  cargo (Rust)
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition Cargo() => new("cargo", "Rust package manager and build tool",
        Subcommands: new CliCommand[]
        {
            new("build",   "Compile the project", Flags: new CliFlag[]
            {
                new("--release",  Description: "Build in release mode"),
                new("--target",   Description: "Target triple",          ExpectsValue: true),
                new("--features", Description: "Enable features",       ExpectsValue: true),
                new("--all-features", Description: "Enable all features"),
                new("--no-default-features", Description: "Disable default features"),
                new("--jobs",     "-j", "Number of parallel jobs",       ExpectsValue: true),
            }),
            new("run",     "Build and execute", Flags: new CliFlag[]
            {
                new("--release",  Description: "Run in release mode"),
                new("--example",  Description: "Run an example",        ExpectsValue: true),
                new("--bin",      Description: "Run a binary",          ExpectsValue: true),
            }),
            new("test",    "Run tests", Flags: new CliFlag[]
            {
                new("--release",    Description: "Test in release mode"),
                new("--no-run",     Description: "Compile but don't run"),
                new("--doc",        Description: "Test documentation examples"),
                new("--lib",        Description: "Test library only"),
            }),
            new("check",   "Check the project compiles without building"),
            new("clean",   "Remove build artifacts"),
            new("doc",     "Build documentation", Flags: new CliFlag[]
            {
                new("--open",      Description: "Open in browser"),
                new("--no-deps",   Description: "Skip dependency docs"),
            }),
            new("new",     "Create a new Cargo package", Flags: new CliFlag[]
            {
                new("--lib",    Description: "Create a library"),
                new("--bin",    Description: "Create a binary (default)"),
                new("--name",   Description: "Package name",      ExpectsValue: true),
                new("--edition", Description: "Rust edition",     ExpectsValue: true),
            }),
            new("init",    "Create a new Cargo package in current dir"),
            new("add",     "Add dependencies", Flags: new CliFlag[]
            {
                new("--dev",       "-D", "Add as dev dependency"),
                new("--build",     "-B", "Add as build dependency"),
                new("--features",  "-F", "Enable features",         ExpectsValue: true),
                new("--optional",  Description: "Mark as optional"),
            }),
            new("remove",  "Remove dependencies"),
            new("update",  "Update dependencies"),
            new("publish", "Publish to crates.io", Flags: new CliFlag[]
            {
                new("--dry-run",   Description: "Dry run"),
                new("--allow-dirty", Description: "Allow dirty working directory"),
            }),
            new("install", "Install a Rust binary"),
            new("clippy",  "Run Clippy lints", Flags: new CliFlag[]
            {
                new("--fix",  Description: "Auto-fix warnings"),
            }),
            new("fmt",     "Format code"),
            new("bench",   "Run benchmarks"),
            new("tree",    "Display dependency tree"),
        },
        GlobalFlags: new CliFlag[]
        {
            new("--help",      "-h", "Show help"),
            new("--version",   "-V", "Show version"),
            new("--verbose",   "-v", "Be verbose"),
            new("--quiet",     "-q", "Suppress output"),
        });

    // ═════════════════════════════════════════════════════════════════════════
    //  go
    // ═════════════════════════════════════════════════════════════════════════

    private static CliToolDefinition Go() => new("go", "Go programming language tool",
        Subcommands: new CliCommand[]
        {
            new("build",    "Compile packages and dependencies", Flags: new CliFlag[]
            {
                new("-o",         Description: "Output binary name",    ExpectsValue: true),
                new("-race",      Description: "Enable race detector"),
                new("-ldflags",   Description: "Linker flags",          ExpectsValue: true),
                new("-tags",      Description: "Build tags",            ExpectsValue: true),
                new("-trimpath",  Description: "Remove file system paths from binary"),
            }),
            new("run",      "Compile and run a Go program"),
            new("test",     "Run package tests", Flags: new CliFlag[]
            {
                new("-v",         Description: "Verbose output"),
                new("-run",       Description: "Run specific tests",    ExpectsValue: true),
                new("-bench",     Description: "Run benchmarks",        ExpectsValue: true),
                new("-count",     Description: "Run count times",       ExpectsValue: true),
                new("-race",      Description: "Enable race detector"),
                new("-cover",     Description: "Enable coverage"),
                new("-coverprofile", Description: "Write coverage profile", ExpectsValue: true),
                new("-timeout",   Description: "Test timeout",          ExpectsValue: true),
                new("-short",     Description: "Run short tests only"),
            }),
            new("fmt",      "Gofmt (reformat) package sources"),
            new("vet",      "Report likely mistakes in packages"),
            new("get",      "Download and install packages", Flags: new CliFlag[]
            {
                new("-u",    Description: "Update modules"),
                new("-d",    Description: "Download only"),
            }),
            new("mod",      "Module maintenance", Subcommands: new CliCommand[]
            {
                new("init",     "Initialize go.mod"),
                new("tidy",     "Add missing and remove unused modules"),
                new("download", "Download modules to local cache"),
                new("vendor",   "Make vendored copy of dependencies"),
                new("graph",    "Print module dependency graph"),
                new("verify",   "Verify dependencies"),
                new("why",      "Explain why packages are needed"),
                new("edit",     "Edit go.mod"),
            }),
            new("install",  "Compile and install packages"),
            new("clean",    "Remove object files and cached files"),
            new("env",      "Print Go environment information"),
            new("generate", "Generate Go files by processing source"),
            new("doc",      "Show documentation for a package"),
            new("list",     "List packages or modules"),
            new("work",     "Workspace maintenance", Subcommands: new CliCommand[]
            {
                new("init",  "Initialize workspace"),
                new("use",   "Add module to workspace"),
                new("sync",  "Sync workspace build list"),
            }),
        },
        GlobalFlags: new CliFlag[]
        {
            new("version",  Description: "Print Go version"),
            new("help",     Description: "Show help"),
        });
}
