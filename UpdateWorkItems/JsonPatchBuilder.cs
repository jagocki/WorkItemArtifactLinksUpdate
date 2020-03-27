using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace UpdateWorkItems
{
    public class JsonPatchBuilder
    {
        public static JsonPatchDocument CreateDeleteArtifactLinkPatch(int rev, int artifactLinkIndex)
        {
            JsonPatchDocument patchDocument = new JsonPatchDocument();

            patchDocument.Add(
               new JsonPatchOperation()
               {
                   Operation = Operation.Test,
                   Path = "/rev", 
                   Value = rev
               }
            );

            patchDocument.Add(
                new JsonPatchOperation()
                {
                    Operation = Operation.Remove,
                    Path = "/relations/" + artifactLinkIndex.ToString()
                });
            return patchDocument;
        }
    }
}
