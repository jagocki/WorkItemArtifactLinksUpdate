﻿using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
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
                    "Azure DevOps Server collection url with both team projects"),
                new Option<bool>(
                    "--validate-only", () => true,
                    "Indicates if the links should be deleted, or if the selection logic only should be run"),
                new Option<string>(
                    "--PAT", () => string.Empty,
                    "PAT for Azure DevOps authentication")
            };
            
            rootCommand.Description = "Quick fix application for work items artifacts links targeting the wrong repository";

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

                DumpWorkItemsArtifactLinks2(workitemsSourceProject, targetTeamProject,targetRepoName, collectionUrl, csvOutputPath, validateOnly);
            });

            return rootCommand.Invoke(args);

        }

        private static void DumpWorkItemsArtifactLinks2(string workitemSource, string targetTeamProject, string targetRepoName, string collectionUrl, string csvFilePath, bool validateOnly)
        {
            //samples: https://github.com/microsoft/azure-devops-dotnet-samples/blob/master/ClientLibrary/Quickstarts/dotnet/WitQuickStarts/Samples/CreateBug.cs

            WorkItemArtifactLinksProcessor processor = new WorkItemArtifactLinksProcessor();
            processor.CreateClients(collectionUrl);
            processor.RangeIncrement = 200;
            processor.WorkItemQuery = "select [ID] from workitems where [ID] = 4";
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


        private static void DumpWorkItemsArtifactLinks(string workitemSource, string targetTeamProject, string targetRepoName, string collectionUrl,string csvFilePath)
        {
            //sample to follow
            //https://github.com/microsoft/azure-devops-dotnet-samples/blob/master/ClientLibrary/Quickstarts/dotnet/WitQuickStarts/Samples/CreateBug.cs

            VssConnection connection = GetConnection(collectionUrl);
            WorkItemTrackingHttpClient workItemTrackingHttpClient = connection.GetClient<WorkItemTrackingHttpClient>();
            ProjectHttpClient projectHttpClient = connection.GetClient<ProjectHttpClient>();


            List<ArtifactLinkRecord> records = new List<ArtifactLinkRecord>();

            var wiIds = GetWorkItemsIds(workItemTrackingHttpClient);
            GitHttpClient gitClient = connection.GetClient<GitHttpClient>();
            int startIndex = 0;
            int rangeIncrement = 200;
            do
            {
                var tempids = wiIds.GetRange(startIndex, Math.Min(rangeIncrement,wiIds.Count-1-startIndex));
                startIndex += rangeIncrement;
                foreach (var item in workItemTrackingHttpClient.GetWorkItemsAsync(tempids, expand: WorkItemExpand.Relations).Result)
                {

                    
                    var gitCommits = item.Relations.Where(element => element.Rel == "ArtifactLink");
                    
                    var targetGitRepoDetails = gitClient.GetRepositoryAsync(targetTeamProject, targetRepoName).Result;
                    TeamProject targetProjectDetails = GetprojectDetails(projectHttpClient, targetTeamProject);


                    foreach (var artifactLink in gitCommits)
                    {
                        if (artifactLink.Url.Contains("Git"))
                        {
                            string[] ids = WebUtility.UrlDecode(artifactLink.Url).Split('/');
                            if (ids[4] == "Commit")
                            {
                                string commitID = ids[7];
                                Guid reposotoryID = Guid.Parse(ids[6]);
                                Guid projectId = Guid.Parse(ids[5]);
                                string projectName = GetProjectName(projectHttpClient, projectId.ToString());
                                bool isDeletedRepo = false;
                                bool isDestroyedRepo = false;
                                bool isTargetingExpectedRepo = false;
                                bool isTargetingExpectedProject = false;
                                string repoName = "";

                                if (string.IsNullOrEmpty(projectName))
                                {
                                    isDeletedRepo = true;
                                    isDestroyedRepo = true;
                                    isTargetingExpectedRepo = false;
                                    isTargetingExpectedProject = false;
                                }
                                else
                                {
                                    var activeProjectRepos = gitClient.GetRepositoriesAsync(projectId).Result;

                                    var linkedActiveRepo = (from r in activeProjectRepos where r.Id == reposotoryID select r).SingleOrDefault();
                                    if (linkedActiveRepo == null)
                                    {
                                        var deletedRepos = gitClient.GetDeletedRepositoriesAsync(projectId).Result;
                                        var deletedLinkedRepo = (from r in deletedRepos where r.Id == reposotoryID select r).SingleOrDefault();
                                        if (deletedLinkedRepo != null)
                                        {
                                            repoName = deletedLinkedRepo.Name;
                                            isDeletedRepo = true;
                                        }
                                        else
                                        {
                                            isDestroyedRepo = true;
                                        }
                                    }
                                    else
                                    {
                                        repoName = linkedActiveRepo.Name;
                                        if (repoName == targetRepoName)
                                        {
                                            isTargetingExpectedRepo = true;
                                        }
                                        if (targetProjectDetails.Id == projectId)
                                        {
                                            isTargetingExpectedProject = true;
                                        }
                                    }
                                }

                                GitCommit commitDetails = null;
                                //var repo = gitClient.GetRepositoryAsync(repositoryId).Result;
                                if (isDeletedRepo == false && isDestroyedRepo == false)
                                {
                                    commitDetails = gitClient.GetCommitAsync(projectId, commitID, reposotoryID).Result;
                                }
                                ArtifactLinkRecord record = new ArtifactLinkRecord
                                {
                                    WorkItemID = item.Id,
                                    WorkItemUrl = item.Url,
                                    ArtifactUri = artifactLink.Url,
                                    commitID = commitID,
                                    IsDeletedRepo = isDeletedRepo,
                                    isDestroyedRepo = isDestroyedRepo,
                                    isTargetingExpectedProject = isTargetingExpectedProject,
                                    isTargetingExpectedRepo = isTargetingExpectedRepo,
                                    LinkedProjectName = projectName,
                                    LinkedRepoId = reposotoryID,
                                    LinkedRepoName = repoName,
                                    shortCommitID = commitID.Substring(0, 8)
                                };

                                records.Add(record);
                                Console.WriteLine($"workItemID={item.Id} projectName={projectName} repoName={repoName}, isDeletedRepo={isDeletedRepo}, isDestroyedRepo={isDestroyedRepo}, gitRepoId={reposotoryID}, commitId={commitID.Substring(0, 8)} ");// , commitComment=link={artifactLink.Url} {commitDetails.Comment}");
                                //https://github.com/microsoft/azure-devops-dotnet-samples/blob/master/ClientLibrary/Samples/WorkItemTracking/WorkItemsSample.cs
                                if (isDestroyedRepo || isDeletedRepo)
                                {
                                    JsonPatchDocument patchDocument = JsonPatchBuilder.CreateDeleteArtifactLinkPatch(item.Rev.Value, item.Relations.IndexOf(artifactLink));
                                    WorkItem result = workItemTrackingHttpClient.UpdateWorkItemAsync(patchDocument, item.Id.GetValueOrDefault()).Result;
                                }
                            }
                        }
                    }
                }
            } while (startIndex  < wiIds.Count -1);

            using (var writer = new StreamWriter(csvFilePath))
            {
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(records);
                }
            }
        }

        private static WorkItemQueryResult GetWorkItemsWithArtifactLinks(WorkItemTrackingHttpClient client)
        {
            Wiql wiql = new Wiql()
            {
                Query = "select [ID] from workitems where [External link count] > 0"
            };
            return client.QueryByWiqlAsync(wiql).Result;

        }

        private static List<int> GetWorkItemsIds(WorkItemTrackingHttpClient client)
        {
            WorkItemQueryResult workItemQueryResult = GetWorkItemsWithArtifactLinks(client);
            List<int> list = new List<int>();

            if (workItemQueryResult.WorkItems.Count() != 0)
            {
                foreach (var item in workItemQueryResult.WorkItems)
                {
                    list.Add(item.Id);
                }

            }

            return list;

        }

            private static VssConnection GetConnection(string collectionUrl)
        {
            //return new VssConnection(new Uri(collectionUrl), new VssBasicCredential("PAT", "PAT"));
            return new VssConnection(new Uri(collectionUrl), new WindowsCredential(true));
        }

        private static string GetProjectName(ProjectHttpClient projectHttpClient, string projectID)
        {
            string projectName = string.Empty;
            try
            {
                projectName = GetprojectDetails(projectHttpClient, projectID).Name;
            }
            catch(AggregateException ex)
            {
                Console.WriteLine($"could not get project name for id={projectID} ");
                Console.WriteLine(ex.Message);
            }
            return projectName;
        }

        private static TeamProject GetprojectDetails(ProjectHttpClient projectHttpClient, string projectID)
        {
            return projectHttpClient.GetProject(projectID).Result;
        }
    }
}