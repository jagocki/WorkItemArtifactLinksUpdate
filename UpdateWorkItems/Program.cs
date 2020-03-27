using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Linq;
using System.Net;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.WebApi.Patch;

namespace UpdateWorkItems
{
    class Program
    {
        static int Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<string>(
                    "--workitems-source-project", () => "scrum",
                    description: "Name or Guid of the team projects with work items having the invalid Git Commits artifact links"),
                new Option<string>(
                    "--target-teamproject", () => "scrum_source",
                    "Team proejct name or guid with Git repo that the work items should target"),
                new Option<string>(
                    "--target-repo-name", () => "scrum_source",
                    "Name of Git repo that the work items should target"),
                new Option<string>(
                    "--csv-output-path", () => @"C:\Users\adam.ALMLAB\Source\Repos\UpdateWorkItems\UpdateWorkItems\bin\Debug\netcoreapp3.0\artifactsLinks.csv",
                    "CSV file to store infomation about ht eartifact links"),
                new Option<string>(
                    "--collection-url", () => "http://almlab-tfs/DefaultCollection",
                    "Azure DevOps Server collection or Azure DevOps organization url with both team projects"),
                new Option<bool>(
                    "--validate-only", () => false,
                    "Indicates if the links should be deleted, or if the selection logic only should be run"),
                new Option<string>(
                    "--PAT", () => string.Empty,
                    "PAT for Azure DevOps authentication")
            };
            
            rootCommand.Description = "Quick fix application for work items artifacts links targeting the wrong repository or project";

            rootCommand.Handler = CommandHandler.Create<string, string, string, string, string, bool, string>((workitemsSourceProject, targetTeamProject, targetRepoName, collectionUrl, csvOutputPath, validateOnly, PAT) =>
            {
                Console.WriteLine($"The value for --workitems-source-project is: {workitemsSourceProject}");
                Console.WriteLine($"The value for --target-teamproject is: {targetTeamProject}");
                Console.WriteLine($"The value for --target-repo-name is: {targetRepoName}");
                Console.WriteLine($"The value for --collection-url is: {collectionUrl}");
                Console.WriteLine($"The value for --csv-output-path is: {csvOutputPath}");
                Console.WriteLine($"The value for --validate-only is: {validateOnly}");
                string patString = PAT == string.Empty ? string.Empty : "*****";
                Console.WriteLine($"The value for --PAT is: {patString}");

                DeleteWrongWorkItemArtifactLinks(workitemsSourceProject, targetTeamProject,targetRepoName, collectionUrl, csvOutputPath, validateOnly);
            });

            return rootCommand.Invoke(args);

        }

        private static void DeleteWrongWorkItemArtifactLinks(string workitemSource, string targetTeamProject, string targetRepoName, string collectionUrl, string csvFilePath, bool validateOnly)
        {
            //samples: https://github.com/microsoft/azure-devops-dotnet-samples/blob/master/ClientLibrary/Quickstarts/dotnet/WitQuickStarts/Samples/CreateBug.cs

            WorkItemArtifactLinksProcessor processor = new WorkItemArtifactLinksProcessor();
            processor.CreateClients(collectionUrl);
            processor.RangeIncrement = 200;
            //processor.WorkItemQuery = "select [ID] from workitems where [ID] = 406";
            var records = processor.GetWorkItemArtifactsLinks(workitemSource, targetTeamProject, targetRepoName);
            processor.DeleteArtifactLinks(records, item => item.IsDeletedRepo || item.isDestroyedRepo, validateOnly);
            using (var writer = new StreamWriter(csvFilePath))
            {
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(records);
                }
            }

        }

    }
}