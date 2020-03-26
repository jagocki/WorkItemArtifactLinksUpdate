using System;
using System.Collections.Generic;
using System.Text;

namespace UpdateWorkItems
{
    public class ArtifactLinkCsvRecord
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
        public string shortCommitID
        {
            get;set;
        }
        public string ArtifactUri
        {
            get;set;
        }
    }
}
