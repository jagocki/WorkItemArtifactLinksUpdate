using System;
using System.Collections.Generic;
using System.Text;

namespace UpdateWorkItems
{
    public class ProjectStatus
    {
        public bool IsDeletedRepo { get; set; }
        public bool IsDestroyedRepo { get; set; }
        public bool IsTargetingExpectedRepo { get; set; }
        public bool IsTargetingExpectedProject { get; set; }

        public string ProjectName { get; set; }

        public string RepoName { get; set; }
    }
}
