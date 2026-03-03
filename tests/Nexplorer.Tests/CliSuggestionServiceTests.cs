using Nexplorer.App.Services;
using Nexplorer.App.ViewModels;

namespace Nexplorer.Tests;

public class CliSuggestionServiceTests
{
    // ═════════════════════════════════════════════════════════════════════════
    //  Tool name matching (single token — Text is just the tool name)
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("gi",    "git")]
    [InlineData("dot",   "dotnet")]
    [InlineData("aw",    "aws")]
    [InlineData("az",    "az")]
    [InlineData("doc",   "docker")]
    [InlineData("kub",   "kubectl")]
    [InlineData("ter",   "terraform")]
    [InlineData("car",   "cargo")]
    public void PartialToolName_SuggestsMatchingTool(string input, string expected)
    {
        var results = CliSuggestionService.GetSuggestions(input);
        Assert.Contains(results, r => r.Text == expected);
        Assert.All(results, r => Assert.Equal(SuggestionKind.CliTool, r.Kind));
    }

    [Fact]
    public void UnknownToolPrefix_ReturnsEmpty()
    {
        var results = CliSuggestionService.GetSuggestions("xyznotool");
        Assert.Empty(results);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Git subcommands — Text is full command (appended to input prefix)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Git_WithSpace_SuggestsSubcommands()
    {
        var results = CliSuggestionService.GetSuggestions("git ");
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Text == "git commit");
        Assert.Contains(results, r => r.Text == "git push");
        Assert.Contains(results, r => r.Text == "git pull");
        Assert.Contains(results, r => r.Text == "git status");
    }

    [Theory]
    [InlineData("git co", "git commit")]
    [InlineData("git co", "git config")]
    [InlineData("git st", "git status")]
    [InlineData("git st", "git stash")]
    public void Git_PartialSubcommand_FiltersCorrectly(string input, string expected)
    {
        var results = CliSuggestionService.GetSuggestions(input);
        Assert.Contains(results, r => r.Text == expected);
    }

    [Fact]
    public void Git_Commit_Flags()
    {
        var results = CliSuggestionService.GetSuggestions("git commit ");
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Text == "git commit --message");
        Assert.Contains(results, r => r.Text == "git commit --amend");
    }

    [Fact]
    public void Git_Commit_PartialFlag()
    {
        var results = CliSuggestionService.GetSuggestions("git commit --m");
        Assert.Contains(results, r => r.Text == "git commit --message");
        Assert.DoesNotContain(results, r => r.Text == "git commit --amend");
    }

    [Fact]
    public void Git_Stash_Subcommands()
    {
        var results = CliSuggestionService.GetSuggestions("git stash ");
        Assert.Contains(results, r => r.Text == "git stash push");
        Assert.Contains(results, r => r.Text == "git stash pop");
        Assert.Contains(results, r => r.Text == "git stash list");
    }

    [Fact]
    public void Git_UsedFlagsNotResuggested()
    {
        var results = CliSuggestionService.GetSuggestions("git commit --amend ");
        Assert.DoesNotContain(results, r => r.Text.EndsWith("--amend"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Dotnet subcommands
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Dotnet_SuggestsSubcommands()
    {
        var results = CliSuggestionService.GetSuggestions("dotnet ");
        Assert.Contains(results, r => r.Text == "dotnet build");
        Assert.Contains(results, r => r.Text == "dotnet run");
        Assert.Contains(results, r => r.Text == "dotnet test");
        Assert.Contains(results, r => r.Text == "dotnet publish");
    }

    [Fact]
    public void Dotnet_Build_Flags()
    {
        var results = CliSuggestionService.GetSuggestions("dotnet build ");
        Assert.Contains(results, r => r.Text == "dotnet build --configuration");
        Assert.Contains(results, r => r.Text == "dotnet build --no-restore");
    }

    [Fact]
    public void Dotnet_Add_Subcommands()
    {
        var results = CliSuggestionService.GetSuggestions("dotnet add ");
        Assert.Contains(results, r => r.Text == "dotnet add package");
        Assert.Contains(results, r => r.Text == "dotnet add reference");
    }

    [Fact]
    public void Dotnet_Ef_Migrations_ThreeLevelNesting()
    {
        var results = CliSuggestionService.GetSuggestions("dotnet ef migrations ");
        Assert.Contains(results, r => r.Text == "dotnet ef migrations add");
        Assert.Contains(results, r => r.Text == "dotnet ef migrations remove");
        Assert.Contains(results, r => r.Text == "dotnet ef migrations list");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  AWS subcommands
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Aws_SuggestsServices()
    {
        var results = CliSuggestionService.GetSuggestions("aws ");
        Assert.Contains(results, r => r.Text == "aws s3");
        Assert.Contains(results, r => r.Text == "aws ec2");
        Assert.Contains(results, r => r.Text == "aws lambda");
    }

    [Fact]
    public void Aws_S3_SuggestsOperations()
    {
        var results = CliSuggestionService.GetSuggestions("aws s3 ");
        Assert.Contains(results, r => r.Text == "aws s3 cp");
        Assert.Contains(results, r => r.Text == "aws s3 sync");
        Assert.Contains(results, r => r.Text == "aws s3 ls");
    }

    [Fact]
    public void Aws_S3_Cp_Flags()
    {
        var results = CliSuggestionService.GetSuggestions("aws s3 cp --");
        Assert.Contains(results, r => r.Text == "aws s3 cp --recursive");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Azure CLI subcommands
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Az_SuggestsSubcommands()
    {
        var results = CliSuggestionService.GetSuggestions("az ");
        Assert.Contains(results, r => r.Text == "az login");
        Assert.Contains(results, r => r.Text == "az group");
        Assert.Contains(results, r => r.Text == "az vm");
        Assert.Contains(results, r => r.Text == "az aks");
    }

    [Fact]
    public void Az_Aks_Subcommands()
    {
        var results = CliSuggestionService.GetSuggestions("az aks ");
        Assert.Contains(results, r => r.Text == "az aks create");
        Assert.Contains(results, r => r.Text == "az aks get-credentials");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  AzCopy subcommands
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AzCopy_SuggestsSubcommands()
    {
        var results = CliSuggestionService.GetSuggestions("azcopy ");
        Assert.Contains(results, r => r.Text == "azcopy copy");
        Assert.Contains(results, r => r.Text == "azcopy sync");
        Assert.Contains(results, r => r.Text == "azcopy remove");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Docker subcommands
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Docker_SuggestsSubcommands()
    {
        var results = CliSuggestionService.GetSuggestions("docker ", maxItems: 20);
        Assert.Contains(results, r => r.Text == "docker build");
        Assert.Contains(results, r => r.Text == "docker run");
        Assert.Contains(results, r => r.Text == "docker ps");
        Assert.Contains(results, r => r.Text == "docker compose");
    }

    [Fact]
    public void Docker_Compose_Subcommands()
    {
        var results = CliSuggestionService.GetSuggestions("docker compose ");
        Assert.Contains(results, r => r.Text == "docker compose up");
        Assert.Contains(results, r => r.Text == "docker compose down");
        Assert.Contains(results, r => r.Text == "docker compose build");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Kubectl subcommands
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Kubectl_SuggestsSubcommands()
    {
        var results = CliSuggestionService.GetSuggestions("kubectl ");
        Assert.Contains(results, r => r.Text == "kubectl get");
        Assert.Contains(results, r => r.Text == "kubectl apply");
        Assert.Contains(results, r => r.Text == "kubectl logs");
    }

    [Fact]
    public void Kubectl_Get_Resources()
    {
        var results = CliSuggestionService.GetSuggestions("kubectl get ");
        Assert.Contains(results, r => r.Text == "kubectl get pods");
        Assert.Contains(results, r => r.Text == "kubectl get deployments");
        Assert.Contains(results, r => r.Text == "kubectl get services");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  SSH flags
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Ssh_SuggestsFlags()
    {
        var results = CliSuggestionService.GetSuggestions("ssh -");
        Assert.Contains(results, r => r.Text == "ssh -p");
        Assert.Contains(results, r => r.Text == "ssh -i");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Terraform subcommands
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Terraform_SuggestsSubcommands()
    {
        var results = CliSuggestionService.GetSuggestions("terraform ");
        Assert.Contains(results, r => r.Text == "terraform init");
        Assert.Contains(results, r => r.Text == "terraform plan");
        Assert.Contains(results, r => r.Text == "terraform apply");
        Assert.Contains(results, r => r.Text == "terraform destroy");
    }

    [Fact]
    public void Terraform_State_Subcommands()
    {
        var results = CliSuggestionService.GetSuggestions("terraform state ");
        Assert.Contains(results, r => r.Text == "terraform state list");
        Assert.Contains(results, r => r.Text == "terraform state show");
        Assert.Contains(results, r => r.Text == "terraform state mv");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  npm subcommands
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Npm_SuggestsSubcommands()
    {
        var results = CliSuggestionService.GetSuggestions("npm ");
        Assert.Contains(results, r => r.Text == "npm install");
        Assert.Contains(results, r => r.Text == "npm run");
        Assert.Contains(results, r => r.Text == "npm test");
    }

    [Fact]
    public void Npm_Install_Flags()
    {
        var results = CliSuggestionService.GetSuggestions("npm install --");
        Assert.Contains(results, r => r.Text == "npm install --save-dev");
        Assert.Contains(results, r => r.Text == "npm install --global");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Cargo and Go
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Cargo_SuggestsSubcommands()
    {
        var results = CliSuggestionService.GetSuggestions("cargo ");
        Assert.Contains(results, r => r.Text == "cargo build");
        Assert.Contains(results, r => r.Text == "cargo run");
        Assert.Contains(results, r => r.Text == "cargo test");
    }

    [Fact]
    public void Go_SuggestsSubcommands()
    {
        var results = CliSuggestionService.GetSuggestions("go ");
        Assert.Contains(results, r => r.Text == "go build");
        Assert.Contains(results, r => r.Text == "go run");
        Assert.Contains(results, r => r.Text == "go test");
        Assert.Contains(results, r => r.Text == "go mod");
    }

    [Fact]
    public void Go_Mod_Subcommands()
    {
        var results = CliSuggestionService.GetSuggestions("go mod ");
        Assert.Contains(results, r => r.Text == "go mod init");
        Assert.Contains(results, r => r.Text == "go mod tidy");
        Assert.Contains(results, r => r.Text == "go mod download");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  pip / python
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Pip_SuggestsSubcommands()
    {
        var results = CliSuggestionService.GetSuggestions("pip ");
        Assert.Contains(results, r => r.Text == "pip install");
        Assert.Contains(results, r => r.Text == "pip freeze");
        Assert.Contains(results, r => r.Text == "pip list");
    }

    [Fact]
    public void Python_SuggestsFlags()
    {
        var results = CliSuggestionService.GetSuggestions("python -");
        Assert.Contains(results, r => r.Text == "python -m");
        Assert.Contains(results, r => r.Text == "python -c");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Edge cases
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyOrWhitespace_ReturnsEmpty(string? input)
    {
        var results = CliSuggestionService.GetSuggestions(input!);
        Assert.Empty(results);
    }

    [Fact]
    public void UnknownTool_ReturnsEmpty()
    {
        var results = CliSuggestionService.GetSuggestions("foobar ");
        Assert.Empty(results);
    }

    [Fact]
    public void GlobalFlags_AlwaysAvailable()
    {
        var results = CliSuggestionService.GetSuggestions("git commit --h");
        Assert.Contains(results, r => r.Text == "git commit --help");
    }

    [Fact]
    public void AllSuggestionsAreCliToolKind()
    {
        var results = CliSuggestionService.GetSuggestions("git ");
        Assert.All(results, r => Assert.Equal(SuggestionKind.CliTool, r.Kind));
    }

    [Fact]
    public void SuggestionsHaveDescriptions()
    {
        var results = CliSuggestionService.GetSuggestions("git ");
        Assert.All(results, r => Assert.False(string.IsNullOrEmpty(r.Detail),
            $"Suggestion '{r.Text}' is missing a description"));
    }

    [Fact]
    public void FlagWithShortName_MatchesByShortName()
    {
        var results = CliSuggestionService.GetSuggestions("git commit -m");
        Assert.Contains(results, r => r.Text == "git commit --message");
    }

    [Fact]
    public void SkipsFlags_WhenNavigatingSubcommands()
    {
        var results = CliSuggestionService.GetSuggestions("aws --region us-east-1 s3 ");
        Assert.Contains(results, r => r.Text == "aws --region us-east-1 s3 cp");
        Assert.Contains(results, r => r.Text == "aws --region us-east-1 s3 ls");
    }

    [Fact]
    public void MaxItems_Respected()
    {
        var results = CliSuggestionService.GetSuggestions("git ", maxItems: 3);
        Assert.True(results.Count <= 3);
    }

    [Fact]
    public void SuggestionText_StartsWithInput()
    {
        // Every CLI suggestion should start with the original input prefix
        var results = CliSuggestionService.GetSuggestions("git commit ");
        Assert.All(results, r => Assert.StartsWith("git commit ", r.Text));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Integration: CompletionService includes CLI suggestions
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CompletionService_IncludesCliSuggestions()
    {
        var history = Array.Empty<CommandHistoryEntry>();
        var results = CompletionService.GetSuggestions("git com", "C:\\", history);
        Assert.Contains(results, r => r.Kind == SuggestionKind.CliTool && r.Text == "git commit");
    }
}
