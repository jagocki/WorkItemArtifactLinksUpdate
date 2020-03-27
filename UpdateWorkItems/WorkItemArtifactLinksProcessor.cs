using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace UpdateWorkItems
{
    public class WorkItemArtifactLinksProcessor
    {
        public WorkItemArtifactLinksProcessor()
        {
            WorkItemQuery = "select [ID] from workitems where [External link count] > 0";
        }

        public string PAT {
            get; set;
        }
        public int RangeIncrement
        {
            get;set;
        }

        public string WorkItemQuery
        {
            get;set;
        }

        private VssConnection AzDConnection;
        private WorkItemTrackingHttpClient workItemTrackingHttpClient;
        ProjectHttpClient projectHttpClient;
        GitHttpClient gitClient;

        public void CreateClients(string collectionUrl)
        {
            AzDConnection = SetUpConnection(collectionUrl);
            workItemTrackingHttpClient = AzDConnection.GetClient<WorkItemTrackingHttpClient>();
            projectHttpClient = AzDConnection.GetClient<ProjectHttpClient>();
            gitClient = AzDConnection.GetClient<GitHttpClient>();

        }

        public List<ArtifactLinkRecord> GetWorkItemArtifactsLinks(string workitemSource, string targetTeamProject, string targetRepoName)
        {
            int startIndex = 0;
            List<ArtifactLinkRecord> records = new List<ArtifactLinkRecord>();
            var wiIds = GetWorkItemsIds(workitemSource);

            do
            {
                var tempids = wiIds.GetRange(startIndex, Math.Min(RangeIncrement, wiIds.Count - startIndex));
                startIndex += RangeIncrement;
                records.AddRange(GetWorkItemRangeArtifactLinks(tempids/*, workitemSource*/, targetTeamProject, targetRepoName));
                
            } while (startIndex < wiIds.Count - 1);

            return records;
        }

        private List<ArtifactLinkRecord> GetWorkItemRangeArtifactLinks(List<int> workItemIds/*, string workitemSource*/, string targetTeamProject, string targetRepoName)
        {
            List<ArtifactLinkRecord> records = new List<ArtifactLinkRecord>();

            foreach (var item in workItemTrackingHttpClient.GetWorkItemsAsync(workItemIds, expand: WorkItemExpand.Relations).Result)
            {
                var artifactWorkItemLinks = item.Relations.Where(element => element.Rel == "ArtifactLink");

                foreach (var artifactLink in artifactWorkItemLinks)
                {
                    ArtifactLinkRecord record = ValidateArtifactLink(item, artifactLink, targetTeamProject, targetRepoName);
                    records.Add(record);
                }
                Console.WriteLine($"Analyzing workItemID={item.Id}");
            }

            return records;
        }

        private ArtifactLinkRecord ValidateArtifactLink(WorkItem item, WorkItemRelation artifactLink, string targetTeamProject, string targetRepoName)
        {
            GitArtifactLinkDetails details = new GitArtifactLinkDetails(artifactLink, item.Relations.IndexOf(artifactLink));
            if (details.IsGitLink)
            {
                if (details.LinkType == GitLinkType.Commit)
                {
                    return ValidateTheGitCommitLink(item, targetTeamProject, targetRepoName, details);
                }
                else if (details.LinkType == GitLinkType.PullRequest)
                {
                    return ValidatePullRequestLink(item, targetTeamProject, targetRepoName, details);
                }
            }

            return null;
        }

        private ArtifactLinkRecord ValidatePullRequestLink(WorkItem item, string targetTeamProject, string targetRepoName, GitArtifactLinkDetails linkDetails)
        {
            ProjectStatus projStatus = CheckProjecStatus(targetTeamProject, targetRepoName, linkDetails);
            
            ArtifactLinkRecord record = new ArtifactLinkRecord
            {
                WorkItemID = item.Id,
                WorkItemUrl = item.Url,
                ArtifactUri = linkDetails.Url,
                PullRequestID = linkDetails.PullRequestID,
                IsDeletedRepo = projStatus.IsDeletedRepo,
                isDestroyedRepo = projStatus.IsDestroyedRepo,
                isTargetingExpectedProject = projStatus.IsTargetingExpectedProject,
                isTargetingExpectedRepo = projStatus.IsTargetingExpectedRepo,
                LinkedProjectName = projStatus.ProjectName,
                LinkedRepoId = linkDetails.RepositoryID,
                LinkedRepoName = projStatus.RepoName,
                Rev = item.Rev.Value,
                ArtifactLinkIndex = linkDetails.Index
            };

            return record;
        }

        private ArtifactLinkRecord ValidateTheGitCommitLink(WorkItem item, string targetTeamProject, string targetRepoName, GitArtifactLinkDetails linkDetails)
        {
            ProjectStatus projStatus = CheckProjecStatus(targetTeamProject, targetRepoName, linkDetails);

            ArtifactLinkRecord record = new ArtifactLinkRecord
            {
                WorkItemID = item.Id,
                WorkItemUrl = item.Url,
                ArtifactUri = linkDetails.Url,
                commitID = linkDetails.CommitID,
                IsDeletedRepo = projStatus.IsDeletedRepo,
                isDestroyedRepo = projStatus.IsDestroyedRepo,
                isTargetingExpectedProject = projStatus.IsTargetingExpectedProject,
                isTargetingExpectedRepo = projStatus.IsTargetingExpectedRepo,
                LinkedProjectName = projStatus.ProjectName,
                LinkedRepoId = linkDetails.RepositoryID,
                LinkedRepoName = projStatus.RepoName,
                shortCommitID = linkDetails.CommitID.Substring(0, 8),
                Rev = item.Rev.Value,
                ArtifactLinkIndex = linkDetails.Index
            };

            return record;
        }

        private ProjectStatus CheckProjecStatus(string targetTeamProject, string targetRepoName, GitArtifactLinkDetails linkDetails)
        {
            ProjectStatus projStatus = CheckActiveProject(linkDetails.ProjectId, targetTeamProject);

            if (!string.IsNullOrEmpty(projStatus.ProjectName))
            {
                projStatus = CheckActiveRepos(targetRepoName, linkDetails.ProjectId, linkDetails.RepositoryID, projStatus);
            }

            return projStatus;
        }

        public List<ArtifactLinkRecord> DeleteArtifactLinks(List<ArtifactLinkRecord> records, Func<ArtifactLinkRecord,bool> predicate, bool validateOnly = true)
        {
            List<ArtifactLinkRecord> itemsToDelete = new List<ArtifactLinkRecord>();
            itemsToDelete.AddRange(records.Where(predicate) );
            int revNumber=0;
            int index = 0;
            WorkItem previousWorkItem = null;

            foreach(ArtifactLinkRecord item in itemsToDelete)
            {
                item.SelectedForDeletion = true;
                string action = "Validating attempt";
                if (!validateOnly)
                {
                    action = "Attempting";
                }

                if (previousWorkItem != null && previousWorkItem.Id == item.WorkItemID)
                {
                    index = previousWorkItem.Relations.IndexOf(
                            previousWorkItem.Relations.Where(element => element.Url == item.ArtifactUri).Single()
                            );
                    revNumber = previousWorkItem.Rev.Value;
                }
                else
                {
                    revNumber = item.Rev;
                    index = item.ArtifactLinkIndex;
                }

                JsonPatchDocument patchDocument = JsonPatchBuilder.CreateDeleteArtifactLinkPatch(revNumber, index);
                Console.WriteLine($"{action} to delete workItemID={item.WorkItemID}, revNumber={revNumber}");

                try
                {
                    if (!validateOnly)
                    {
                        previousWorkItem = workItemTrackingHttpClient.UpdateWorkItemAsync(patchDocument, item.WorkItemID.Value).Result;
                        Console.WriteLine($"Deleted workItemID={item.WorkItemID}, artifactLinkIndex={item.ArtifactLinkIndex}");
                        item.Deleted = true;
                    }
                }
                catch(AggregateException ex)
                {
                    //todo - use logger
                    Console.WriteLine("error while deleteing the workitem id =" + item.WorkItemID.Value.ToString());
                    item.Message = ex.Message;
                }
            }
            return itemsToDelete;
        }

        private ProjectStatus CheckActiveProject(Guid projectId, string targetTeamProject)
        {
            ProjectStatus status = new ProjectStatus();
            status.ProjectName = GetProjectName(projectId.ToString());
             
            if (string.IsNullOrEmpty(status.ProjectName))
            {
                status.IsDeletedRepo = true;
                status.IsDestroyedRepo = true;
                status.IsTargetingExpectedRepo = false;
                status.IsTargetingExpectedProject = false;
            }
            else
            {
                if (GetProjectDetails(targetTeamProject).Result.Id == projectId)
                {
                    status.IsTargetingExpectedProject = true;
                }
            }

            return status;
        }

        private ProjectStatus CheckActiveRepos(string targetRepoName, Guid projectId,Guid repositoryID, ProjectStatus status)
        {
            string repoName = string.Empty ;
            var activeProjectRepos = gitClient.GetRepositoriesAsync(projectId).Result;
            var linkedActiveRepo = (from r in activeProjectRepos where r.Id == repositoryID select r).SingleOrDefault();
            if (linkedActiveRepo == null)
            {
                var deletedRepos = gitClient.GetDeletedRepositoriesAsync(projectId).Result;
                var deletedLinkedRepo = (from r in deletedRepos where r.Id == repositoryID select r).SingleOrDefault();
                if (deletedLinkedRepo != null)
                {
                    status.RepoName = deletedLinkedRepo.Name;
                    status.IsDeletedRepo = true;
                }
                else
                {
                    status.IsDestroyedRepo = true;
                }
            }
            else
            {
                status.RepoName = linkedActiveRepo.Name;
                if (status.RepoName == targetRepoName)
                {
                    status.IsTargetingExpectedRepo = true;
                }
            }
            return status;
        }

        private List<int> GetWorkItemsIds(string workitemSource)
        {
            WorkItemQueryResult workItemQueryResult = GetWorkItemsWithArtifactLinks(workitemSource).Result;
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

        private async Task<WorkItemQueryResult> GetWorkItemsWithArtifactLinks(string workitemSource)
        {
            TeamProject projectDetails = await GetProjectDetails(workitemSource);
            Wiql wiql = new Wiql()
            {
                Query = this.WorkItemQuery
            };
            return await workItemTrackingHttpClient.QueryByWiqlAsync(wiql,projectDetails.Id);

        }
        private Task<TeamProject> GetProjectDetails(string projectID)
        {
            return projectHttpClient.GetProject(projectID);
        }

        private string GetProjectName(string projectID)
        {
            string projectName = string.Empty;
            try
            {
                projectName = GetProjectDetails(projectID).Result.Name;
            }
            catch (AggregateException ex)
            {
                //to do - use logger 
                Console.WriteLine($"could not get project name for id={projectID} ");
                Console.WriteLine(ex.Message);
            }

            return projectName;
        }

        private VssConnection SetUpConnection(string collectionUrl)
        {
            if (string.IsNullOrEmpty(PAT))
            {
                return new VssConnection(new Uri(collectionUrl), new WindowsCredential(true));
            }
            else
            {
                return new VssConnection(new Uri(collectionUrl), new VssBasicCredential(PAT, PAT));
            }

        }

       
    }
}
