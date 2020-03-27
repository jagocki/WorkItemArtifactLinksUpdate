using System;
using System.Collections.Generic;
using System.Text;

namespace UpdateWorkItems
{
    public class ArtifactLinkRecord
    {
        public int? WorkItemID
        {
            get;set;
        }
        public string WorkItemUrl
        {
            get;set;
        }
        public bool IsDeletedRepo
        {
            get; set;
        }
        public bool isDestroyedRepo
        {
            get; set;
        }
        public bool isTargetingExpectedRepo
        {
            get; set;
        }
        public bool isTargetingExpectedProject
        {
            get;set;
        }
        public string LinkedProjectName
        {
            get;set;
        }
        public string LinkedRepoName
        {
            get;set;
        }
        public Guid LinkedRepoId
        {
            get;set;
        }
        public string commitID
        {
            get;set;
        }
        public int PullRequestID
        {
            get;set;
        }
        public string shortCommitID
        {
            get;set;
        }
        public string ArtifactUri
        {
            get;set;
        }
        public int Rev
        {
            get;set;
        }
        public int ArtifactLinkIndex
        {
            get;set;
        }
        public bool SelectedForDeletion
        {
            get;set;
        }
        public bool Deleted
        {
            get;set;
        }

        public string Message
        {
            get;set;
        }
    }
}
