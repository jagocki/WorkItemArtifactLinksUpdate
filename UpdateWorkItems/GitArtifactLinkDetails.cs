using Microsoft.TeamFoundation.WorkItemTracking.Process.WebApi.Models;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace UpdateWorkItems
{
    public class GitArtifactLinkDetails
    {
        public GitLinkType LinkType { get; set; }
        public Guid RepositoryID { get; set; }
        public Guid ProjectId { get; set; }
        public bool IsGitLink { get; set; }
        public string CommitID { get; set; }
        public int PullRequestID { get; set; }
        public string Url { get; set; }
        public int Index { get; set; }

        public GitArtifactLinkDetails(WorkItemRelation artifactLink, int linkIndex)
        {
            Url = artifactLink.Url;
            Index = linkIndex;
            if (Url.Contains("Git"))
            {
                string[] ids = WebUtility.UrlDecode(Url).Split('/');

                if (ids[4] == "Commit")
                {
                     IsGitLink = true;
                    CommitID = ids[7];
                    RepositoryID = Guid.Parse(ids[6]);
                    ProjectId = Guid.Parse(ids[5]);
                    LinkType = GitLinkType.Commit;
                }
                else if (ids[4] == "PullRequestId")
                {
                    IsGitLink = true;
                    ProjectId = Guid.Parse(ids[5]);
                    LinkType = GitLinkType.PullRequest;
                    RepositoryID = Guid.Parse(ids[6]);
                    PullRequestID = int.Parse(ids[7]);
                }
                //we don't handle the other cases as for now like 'Ref'links to branches 
                //else
                //{
                //    throw new ArgumentException("Link Type not recognized!");
                //}
            }
        }
    }
}
