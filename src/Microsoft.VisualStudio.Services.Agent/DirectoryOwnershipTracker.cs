using System;
using System.IO;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent
{
    [DataContract]
    public class DirectoryOwnershipInfo
    {
        [DataMember]
        public string ServerUrl { get; set; }

        [DataMember]
        public string PoolName { get; set; }

        [DataMember]
        public string AgentName { get; set; }

        [DataMember]
        public string AgentPath { get; set; }
    }

    public interface IDirectoryOwnershipTracker : IAgentService
    {
        void RegisterDirectoryOwnership(string path);
        void UnRegisterDirectoryOwnership(string path);
        bool IsDirectoryOwneByAgent(string path, bool throwOnNotOwn);
    }

    public sealed class DirectoryOwnershipTracker : AgentService, IDirectoryOwnershipTracker
    {
        public void RegisterDirectoryOwnership(string path)
        {
            // Register ownership always overwrite existing ownership
            Trace.Entering();

            // ensure directory exist
            ArgUtil.Directory(path, nameof(RegisterDirectoryOwnership));

            var configurationStore = HostContext.GetService<IConfigurationStore>();
            var agentSettings = configurationStore.GetSettings();

            // create ownership info 
            DirectoryOwnershipInfo ownership = new DirectoryOwnershipInfo()
            {
                ServerUrl = agentSettings.ServerUrl,
                PoolName = agentSettings.PoolName,
                AgentName = agentSettings.AgentName,
                AgentPath = HostContext.GetDirectory(WellKnownDirectory.Root)
            };

            Trace.Info($"Stamp ownership info for directory: {path}{Environment.NewLine}{StringUtil.ConvertToJson(ownership)}");

            // create .ownership file
            string ownershipFile = IOUtil.GetDirectoryOwnershipFilePath(path);
            if (File.Exists(ownershipFile))
            {
                IOUtil.DeleteFile(ownershipFile);
            }

            IOUtil.SaveObject(ownership, ownershipFile);
            Trace.Info($"Directory ownership tracking created: {ownershipFile}");

            File.SetAttributes(ownershipFile, File.GetAttributes(ownershipFile) | FileAttributes.Hidden);
        }

        public void UnRegisterDirectoryOwnership(string path)
        {
            // Unregister ownership is best effort
            Trace.Entering();
            if (!Directory.Exists(path))
            {
                Trace.Info($"Directory doesn't exist: {path}");
                return;
            }

            string ownershipFile = IOUtil.GetDirectoryOwnershipFilePath(path);
            if (!File.Exists(ownershipFile))
            {
                Trace.Info($"Directory ownership file doesn't exist: {ownershipFile}");
                return;
            }

            if (IsDirectoryOwneByAgent(path, throwOnNotOwn: false))
            {
                Trace.Info($"Remove directory ownership tracking file {ownershipFile}.");
                IOUtil.DeleteFile(ownershipFile);
            }
        }

        public bool IsDirectoryOwneByAgent(string path, bool throwOnNotOwn)
        {
            Trace.Entering();
            try
            {
                ArgUtil.Directory(path, nameof(IsDirectoryOwneByAgent));

                string ownershipFile = IOUtil.GetDirectoryOwnershipFilePath(path);
                ArgUtil.File(ownershipFile, nameof(IsDirectoryOwneByAgent));

                var ownership = IOUtil.LoadObject<DirectoryOwnershipInfo>(ownershipFile);

                var configurationStore = HostContext.GetService<IConfigurationStore>();
                var agentSettings = configurationStore.GetSettings();

                if (!string.Equals(ownership.ServerUrl, agentSettings.ServerUrl, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ownership.PoolName, agentSettings.PoolName, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ownership.AgentName, agentSettings.AgentName, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ownership.AgentPath, HostContext.GetDirectory(WellKnownDirectory.Root), StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception($"Directory '{path}' is own by agent '{ownership.AgentName}' in pool '{ownership.PoolName}' of server '{ownership.ServerUrl}' located at '{HostContext.GetDirectory(WellKnownDirectory.Root)}'.");
                }
            }
            catch (Exception ex) when (!throwOnNotOwn)
            {
                Trace.Error("Catch exception during check directory ownership.");
                Trace.Error(ex);
                return false;
            }

            return true;
        }
    }
}