﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Agent.Sdk;
using Pipelines = Microsoft.TeamFoundation.DistributedTask.Pipelines;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Agent.Plugins.Repository
{
    public interface ISourceProvider
    {
        Task GetSourceAsync(AgentTaskPluginExecutionContext executionContext, Pipelines.RepositoryResource repository, CancellationToken cancellationToken);

        Task PostJobCleanupAsync(AgentTaskPluginExecutionContext executionContext, Pipelines.RepositoryResource repository);
    }

    public abstract class RepositoryTask : IAgentTaskPlugin
    {
        public Guid Id => new Guid("c61807ba-5e20-4b70-bd8c-3683c9f74003");
        public string Version => "1.0.0";

        public abstract string Stage { get; }

        public abstract Task RunAsync(AgentTaskPluginExecutionContext executionContext, CancellationToken token);

        protected ISourceProvider GetSourceProvider(string repositoryType)
        {
            ISourceProvider sourceProvider = null;
            switch (repositoryType)
            {
                case RepositoryTypes.Bitbucket:
                case RepositoryTypes.GitHub:
                case RepositoryTypes.GitHubEnterprise:
                    sourceProvider = new AuthenticatedGitSourceProvider();
                    break;
                case RepositoryTypes.Git:
                    sourceProvider = new ExternalGitSourceProvider();
                    break;
                case RepositoryTypes.TfsGit:
                    sourceProvider = new TfsGitSourceProvider();
                    break;
                case RepositoryTypes.TfsVersionControl:
                    sourceProvider = new TfsVCSourceProvider();
                    break;
                case RepositoryTypes.Svn:
                    sourceProvider = new SvnSourceProvider();
                    break;
                default:
                    throw new NotSupportedException(repositoryType);
            }

            return sourceProvider;
        }
        protected void MergeInputs(AgentTaskPluginExecutionContext executionContext, Pipelines.RepositoryResource repository)
        {
            string clean = executionContext.GetInput("clean");
            if (!string.IsNullOrEmpty(clean))
            {
                repository.Properties.Set<bool>("clean", StringUtil.ConvertToBoolean(clean));
            }

            // there is no addition inputs for TFVC and SVN
            if (repository.Type == RepositoryTypes.Bitbucket ||
                repository.Type == RepositoryTypes.GitHub ||
                repository.Type == RepositoryTypes.GitHubEnterprise ||
                repository.Type == RepositoryTypes.Git ||
                repository.Type == RepositoryTypes.TfsGit)
            {
                string checkoutSubmodules = executionContext.GetInput("checkoutSubmodules");
                if (!string.IsNullOrEmpty(checkoutSubmodules))
                {
                    repository.Properties.Set<bool>("checkoutSubmodules", StringUtil.ConvertToBoolean(checkoutSubmodules));
                }

                string checkoutNestedSubmodules = executionContext.GetInput("checkoutNestedSubmodules");
                if (!string.IsNullOrEmpty(checkoutNestedSubmodules))
                {
                    repository.Properties.Set<bool>("checkoutNestedSubmodules", StringUtil.ConvertToBoolean(checkoutNestedSubmodules));
                }

                string preserveCredential = executionContext.GetInput("preserveCredential");
                if (!string.IsNullOrEmpty(preserveCredential))
                {
                    repository.Properties.Set<bool>("preserveCredential", StringUtil.ConvertToBoolean(preserveCredential));
                }

                string gitLfsSupport = executionContext.GetInput("gitLfsSupport");
                if (!string.IsNullOrEmpty(gitLfsSupport))
                {
                    repository.Properties.Set<bool>("gitLfsSupport", StringUtil.ConvertToBoolean(gitLfsSupport));
                }

                string acceptUntrustedCerts = executionContext.GetInput("acceptUntrustedCerts");
                if (!string.IsNullOrEmpty(acceptUntrustedCerts))
                {
                    repository.Properties.Set<bool>("acceptUntrustedCerts", StringUtil.ConvertToBoolean(acceptUntrustedCerts));
                }

                string fetchDepth = executionContext.GetInput("fetchDepth");
                if (!string.IsNullOrEmpty(fetchDepth))
                {
                    repository.Properties.Set<string>("fetchDepth", fetchDepth);
                }
            }
        }
    }

    public class CheckoutTask : RepositoryTask
    {
        public override string Stage => "main";

        public override async Task RunAsync(AgentTaskPluginExecutionContext executionContext, CancellationToken token)
        {
            var repoAlias = executionContext.GetInput("repository", true);
            var repo = executionContext.Repositories.Single(x => string.Equals(x.Alias, repoAlias, StringComparison.OrdinalIgnoreCase));
            MergeInputs(executionContext, repo);

            ISourceProvider sourceProvider = GetSourceProvider(repo.Type);
            await sourceProvider.GetSourceAsync(executionContext, repo, token);
        }
    }

    public class CleanupTask : RepositoryTask
    {
        public override string Stage => "post";

        public override async Task RunAsync(AgentTaskPluginExecutionContext executionContext, CancellationToken token)
        {
            var repoAlias = executionContext.GetInput("repository", true);
            var repo = executionContext.Repositories.Single(x => string.Equals(x.Alias, repoAlias, StringComparison.OrdinalIgnoreCase));
            MergeInputs(executionContext, repo);

            ISourceProvider sourceProvider = GetSourceProvider(repo.Type);
            await sourceProvider.PostJobCleanupAsync(executionContext, repo);
        }
    }
}
