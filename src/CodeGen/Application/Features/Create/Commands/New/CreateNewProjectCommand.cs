﻿using Core.CodeGen.Code;
using Core.CodeGen.CommandLine.Git;
using Core.CodeGen.File;
using MediatR;
using System.Runtime.CompilerServices;

namespace Application.Features.Create.Commands.New;

public class CreateNewProjectCommand : IStreamRequest<CreatedNewProjectResponse>
{
    public string ProjectName { get; set; }
    public bool IsThereSecurityMechanism { get; set; } = true;

    public CreateNewProjectCommand()
    {
        ProjectName = string.Empty;
    }

    public CreateNewProjectCommand(string projectName, bool isThereSecurityMechanism)
    {
        ProjectName = projectName;
        IsThereSecurityMechanism = isThereSecurityMechanism;
    }

    public class CreateNewProjectCommandHandler
        : IStreamRequestHandler<CreateNewProjectCommand, CreatedNewProjectResponse>
    {
        public async IAsyncEnumerable<CreatedNewProjectResponse> Handle(
            CreateNewProjectCommand request,
            [EnumeratorCancellation] CancellationToken cancellationToken
        )
        {
            CreatedNewProjectResponse response = new();
            List<string> newFilePaths = new();

            response.CurrentStatusMessage = "Cloning starter project and core packages...";
            yield return response;
            response.OutputMessage = null;
            await cloneCorePackagesAndStarterProject(request.ProjectName);
            response.LastOperationMessage =
                "Starter project has been cloned from 'https://github.com/masterbfk/coreTemplate'.";

            response.CurrentStatusMessage = "Preparing project...";
            yield return response;
            await renameProject(request.ProjectName);
            if (!request.IsThereSecurityMechanism)
                await removeSecurityMechanism(request.ProjectName);
            response.LastOperationMessage =
                $"Project has been prepared with {request.ProjectName.ToPascalCase()}.";

            DirectoryHelper.DeleteDirectory(
                $"{Environment.CurrentDirectory}/{request.ProjectName}/.git"
            );
            ICollection<string> newFiles = DirectoryHelper.GetFilesInDirectoryTree(
                root: $"{Environment.CurrentDirectory}/{request.ProjectName}",
                searchPattern: "*"
            );

            response.CurrentStatusMessage = "Initializing git repository with submodules...";
            yield return response;
            await initializeGitRepository(request.ProjectName);
            response.LastOperationMessage = "Git repository has been initialized.";

            response.CurrentStatusMessage = "Completed.";
            response.NewFilePathsResult = newFiles;
            response.OutputMessage =
                $":warning: Check the configuration that has name 'appsettings.json' in 'src/{request.ProjectName.ToCamelCase()}'.";
            response.OutputMessage =
                ":warning: Run 'Update-Database' nuget command on the Persistence layer to apply initial migration.";
            yield return response;
        }

        private async Task cloneCorePackagesAndStarterProject(string projectName) =>
            await GitCommandHelper.RunAsync(
                $"clone https://github.com/masterbfk/coreTemplate.git ./{projectName}"
            );

        private async Task renameProject(string projectName)
        {
            Directory.SetCurrentDirectory($"./{projectName}");

            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/coreTemplate.sln",
                search: "coreTemplate",
                projectName: projectName.ToPascalCase()
            );
            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/coreTemplate.sln.DotSettings",
                search: "coreTemplate",
                projectName: projectName.ToPascalCase()
            );

            string projectPath = $"{Environment.CurrentDirectory}/src/{projectName.ToCamelCase()}";
            Directory.Move(
                sourceDirName: $"{Environment.CurrentDirectory}/src/starterProject",
                projectPath
            );

            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/{projectName.ToPascalCase()}.sln",
                search: "starterProject",
                projectName: projectName.ToCamelCase()
            );

            await replaceFileContentWithProjectName(
                path: $"{Environment.CurrentDirectory}/Application.Tests/Application.Tests.csproj",
                search: "starterProject",
                projectName: projectName.ToCamelCase()
            );

            await replaceFileContentWithProjectName(
                path: $"{projectPath}/WebAPI/appsettings.json",
                search: "StarterProject",
                projectName: projectName.ToPascalCase()
            );
            await replaceFileContentWithProjectName(
                path: $"{projectPath}/WebAPI/appsettings.json",
                search: "starterProject",
                projectName: projectName.ToCamelCase()
            );

            Directory.SetCurrentDirectory("../");

            static async Task replaceFileContentWithProjectName(
                string path,
                string search,
                string projectName
            )
            {
                if (path.Contains(search))
                {
                    string newPath = path.Replace(search, projectName);
                    Directory.Move(path, newPath);
                    path = newPath;
                }

                string fileContent = await File.ReadAllTextAsync(path);
                fileContent = fileContent.Replace(search, projectName);
                await File.WriteAllTextAsync(path, fileContent);
            }
        }

        private async Task removeSecurityMechanism(string projectName)
        {
            string slnPath = $"{Environment.CurrentDirectory}/{projectName.ToPascalCase()}";
            string projectSourcePath = $"{slnPath}/src/{projectName.ToCamelCase()}";
            string projectTestsPath = $"{slnPath}/tests/";

            string[] dirsToDelete = new[]
            {
                $"{projectSourcePath}/Application/Features/Auth",
                $"{projectSourcePath}/Application/Features/OperationClaims",
                $"{projectSourcePath}/Application/Features/UserOperationClaims",
                $"{projectSourcePath}/Application/Features/Users",
                $"{projectSourcePath}/Application/Services/AuthenticatorService",
                $"{projectSourcePath}/Application/Services/AuthService",
                $"{projectSourcePath}/Application/Services/OperationClaims",
                $"{projectSourcePath}/Application/Services/UserOperationClaims",
                $"{projectSourcePath}/Application/Services/UsersService",
                $"{projectTestsPath}/Application.Tests/Features/Users",
            };
            foreach (string dirPath in dirsToDelete)
                Directory.Delete(dirPath, recursive: true);

            string[] filesToDelete = new[]
            {
                $"{projectSourcePath}/Application/Services/Repositories/IEmailAuthenticatorRepository.cs",
                $"{projectSourcePath}/Application/Services/Repositories/IOperationClaimRepository.cs",
                $"{projectSourcePath}/Application/Services/Repositories/IOtpAuthenticatorRepository.cs",
                $"{projectSourcePath}/Application/Services/Repositories/IRefreshTokenRepository.cs",
                $"{projectSourcePath}/Application/Services/Repositories/IUserOperationClaimRepository.cs",
                $"{projectSourcePath}/Application/Services/Repositories/IUserRepository.cs",
                $"{projectSourcePath}/Persistence/EntityConfigurations/EmailAuthenticatorConfiguration.cs",
                $"{projectSourcePath}/Persistence/EntityConfigurations/OperationClaimConfiguration.cs",
                $"{projectSourcePath}/Persistence/EntityConfigurations/OtpAuthenticatorConfiguration.cs",
                $"{projectSourcePath}/Persistence/EntityConfigurations/RefreshTokenConfiguration.cs",
                $"{projectSourcePath}/Persistence/EntityConfigurations/UserConfiguration.cs",
                $"{projectSourcePath}/Persistence/EntityConfigurations/UserOperationClaimConfiguration.cs",
                $"{projectSourcePath}/Persistence/Repositories/EmailAuthenticatorRepository.cs",
                $"{projectSourcePath}/Persistence/Repositories/OperationClaimRepository.cs",
                $"{projectSourcePath}/Persistence/Repositories/OtpAuthenticatorRepository.cs",
                $"{projectSourcePath}/Persistence/Repositories/RefreshTokenRepository.cs",
                $"{projectSourcePath}/Persistence/Repositories/UserOperationClaimRepository.cs",
                $"{projectSourcePath}/Persistence/Repositories/UserRepository.cs",
                $"{projectSourcePath}/WebAPI/Controllers/AuthController.cs",
                $"{projectSourcePath}/WebAPI/Controllers/OperationClaimsController.cs",
                $"{projectSourcePath}/WebAPI/Controllers/UserOperationClaimsController.cs",
                $"{projectSourcePath}/WebAPI/Controllers/UsersController.cs",
                $"{projectSourcePath}/WebAPI/Controllers/Dtos/UpdateByAuthFromServiceRequestDto.cs",
                $"{projectTestsPath}/Application.Tests/DependencyResolvers/UsersTestServiceRegistration.cs",
                $"{projectTestsPath}/Application.Tests/Mocks/FakeData/UserFakeData.cs",
                $"{projectTestsPath}/Application.Tests/Mocks/Repositories/UserMockRepository.cs",
            };
            foreach (string filePath in filesToDelete)
                File.Delete(filePath);

            await FileHelper.RemoveLinesAsync(
                filePath: $"{projectSourcePath}/Application/ApplicationServiceRegistration.cs",
                predicate: line =>
                    (
                        new[]
                        {
                            "using Application.Services.AuthenticatorService;",
                            "using Application.Services.AuthService;",
                            "using Application.Services.UsersService;",
                            "services.AddScoped<IAuthService, AuthManager>();",
                            "services.AddScoped<IAuthenticatorService, AuthenticatorManager>();",
                            "services.AddScoped<IUserService, UserManager>();"
                        }
                    ).Any(line.Contains)
            );
            await FileHelper.RemoveLinesAsync(
                filePath: $"{projectSourcePath}/Persistence/Contexts/BaseDbContext.cs",
                predicate: line =>
                    (
                        new[]
                        {
                            "DbSet<EmailAuthenticator> EmailAuthenticators",
                            "DbSet<OperationClaim> OperationClaim",
                            "DbSet<OtpAuthenticator> OtpAuthenticator",
                            "DbSet<RefreshToken> RefreshTokens",
                            "DbSet<User> User",
                            "DbSet<UserOperationClaim> UserOperationClaims",
                        }
                    ).Any(line.Contains)
            );
            await FileHelper.RemoveLinesAsync(
                filePath: $"{projectSourcePath}/Persistence/PersistenceServiceRegistration.cs",
                predicate: line =>
                    (
                        new[]
                        {
                            "using Persistence.Repositories;",
                            "using Application.Services.Repositories;",
                            "services.AddScoped<IEmailAuthenticatorRepository, EmailAuthenticatorRepository>()",
                            "services.AddScoped<IOperationClaimRepository, OperationClaimRepository>()",
                            "services.AddScoped<IOtpAuthenticatorRepository, OtpAuthenticatorRepository>();",
                            "services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>()",
                            "services.AddScoped<IUserRepository, UserRepository>();",
                            "services.AddScoped<IUserOperationClaimRepository, UserOperationClaimRepository>();",
                        }
                    ).Any(line.Contains)
            );
            await FileHelper.RemoveLinesAsync(
                filePath: $"{projectTestsPath}/Application.Tests/Startup.cs",
                predicate: line =>
                    (
                        new[]
                        {
                            "using Application.Tests.DependencyResolvers;",
                            "public void ConfigureServices(IServiceCollection services) => services.AddUsersServices();",
                        }
                    ).Any(line.Contains)
            );

            await FileHelper.RemoveContentAsync(
                filePath: $"{projectSourcePath}/WebAPI/Program.cs",
                contents: new[]
                {
                    "using Core.Security;",
                    "using Core.Security.Encryption;",
                    "using Core.Security.JWT;",
                    "using Core.WebAPI.Extensions.Swagger;",
                    "using Microsoft.AspNetCore.Authentication.JwtBearer;",
                    "using Microsoft.IdentityModel.Tokens;",
                    "using Microsoft.OpenApi.Models;",
                    "builder.Services.AddSecurityServices();",
                    @"const string tokenOptionsConfigurationSection = ""TokenOptions"";
TokenOptions tokenOptions =
    builder.Configuration.GetSection(tokenOptionsConfigurationSection).Get<TokenOptions>()
    ?? throw new InvalidOperationException($""\""{tokenOptionsConfigurationSection}\"" section cannot found in configuration."");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = tokenOptions.Issuer,
            ValidAudience = tokenOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey)
        };
    });",
                    @"opt.AddSecurityDefinition(
        name: ""Bearer"",
        securityScheme: new OpenApiSecurityScheme
        {
            Name = ""Authorization"",
            Type = SecuritySchemeType.Http,
            Scheme = ""Bearer"",
            BearerFormat = ""JWT"",
            In = ParameterLocation.Header,
            Description =
                ""JWT Authorization header using the Bearer scheme. Example: \""Authorization: Bearer YOUR_TOKEN\"". \r\n\r\n""
                + ""`Enter your token in the text input below.`""
        }
    );
    opt.OperationFilter<BearerSecurityRequirementOperationFilter>();",
                    @"app.UseAuthentication();
app.UseAuthorization();"
                }
            );
        }

        private async Task initializeGitRepository(string projectName)
        {
            Directory.SetCurrentDirectory($"./{projectName}");
            await GitCommandHelper.RunAsync($"init");
            await GitCommandHelper.RunAsync($"branch -m master main");
            //Directory.Delete($"{Environment.CurrentDirectory}/src/corePackages/");
            //await GitCommandHelper.RunAsync(
            //    "submodule add https://github.com/masterbfk/coreTemplate/CoreTemplate.Core ./src/corePackages"
            //);
            //await GitCommandHelper.CommitChangesAsync(
            //    "chore: initial commit"
            //);
            Directory.SetCurrentDirectory("../");
        }
    }
}
