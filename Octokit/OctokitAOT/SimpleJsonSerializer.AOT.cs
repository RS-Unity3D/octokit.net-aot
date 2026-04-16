
namespace Octokit.Internal
{
    #if USE_AOT_JSON
    using System;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using RS.SimpleJsonUnity;
    using RS.Octokit.AOT;

    public class SimpleJsonSerializer : IJsonSerializer
    {
        static SimpleJsonSerializer()
        { 
            
            //注册序列化/反序列化为json的对象的构造器
            RS.SimpleJsonUnity.SimpleJson.InitializeCommonAotTypes();
            // 注册基本集合类型
            //RS.SimpleJsonUnity.SimpleJson.InitializeCommonAotTypes();已经注册了常用的
            //SimpleJson.RegisterAotType(typeof(List<string>), () => new List<string>());
            //SimpleJson.RegisterAotType(typeof(List<int>), () => new List<int>());
            //SimpleJson.RegisterAotType(typeof(List<long>), () => new List<long>());
            //SimpleJson.RegisterAotType(typeof(List<bool>), () => new List<bool>());
            //SimpleJson.RegisterAotType(typeof(Dictionary<string, object>), () => new Dictionary<string, object>());
            //SimpleJson.RegisterAotType(typeof(Dictionary<string, string>), () => new Dictionary<string, string>());
            //标记枚举类型防止裁剪
            ResisterEnumTypes();   
            // 注册常用的 Octokit 模型类型到simplejson构造器
            RegisterCommonOctokitTypes();
        }
        
        static readonly GitHubJsonSerializerStrategy _serializationStrategy = new GitHubJsonSerializerStrategy();
        
        private static void RegisterCommonOctokitTypes()
        {
            try
            {
                //注册
                SimpleJson.RegisterAotType(typeof(Octokit.CheckRunEventPayload),() => new Octokit.CheckRunEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.CheckSuiteEventPayload),() => new Octokit.CheckSuiteEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.CommitCommentPayload),() => new Octokit.CommitCommentPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.CreateEventPayload),() => new Octokit.CreateEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.DeleteEventPayload),() => new Octokit.DeleteEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.ForkEventPayload),() => new Octokit.ForkEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.IssueCommentPayload),() => new Octokit.IssueCommentPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.IssueEventPayload),() => new Octokit.IssueEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.PullRequestEventPayload),() => new Octokit.PullRequestEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.PullRequestReviewEventPayload),() => new Octokit.PullRequestReviewEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.PullRequestCommentPayload),() => new Octokit.PullRequestCommentPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.PushEventPayload),() => new Octokit.PushEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.ReleaseEventPayload),() => new Octokit.ReleaseEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.StatusEventPayload),() => new Octokit.StatusEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.StarredEventPayload),() => new Octokit.StarredEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.ActivityPayload),() => new Octokit.ActivityPayload()); 

                // 注册核心模型类型
                SimpleJson.RegisterAotType(typeof(Octokit.Repository), () => new Octokit.Repository());
                SimpleJson.RegisterAotType(typeof(Octokit.User), () => new Octokit.User());
                SimpleJson.RegisterAotType(typeof(Octokit.Issue), () => new Octokit.Issue());
                SimpleJson.RegisterAotType(typeof(Octokit.PullRequest), () => new Octokit.PullRequest());
                SimpleJson.RegisterAotType(typeof(Octokit.Branch), () => new Octokit.Branch());
                SimpleJson.RegisterAotType(typeof(Octokit.Commit), () => new Octokit.Commit());
                SimpleJson.RegisterAotType(typeof(Octokit.Release), () => new Octokit.Release());
                SimpleJson.RegisterAotType(typeof(Octokit.Label), () => new Octokit.Label());
                SimpleJson.RegisterAotType(typeof(Octokit.Milestone), () => new Octokit.Milestone());               
                SimpleJson.RegisterAotType(typeof(Octokit.Activity), () => new Octokit.Activity());
                SimpleJson.RegisterAotType(typeof(Octokit.EventInfo), () => new Octokit.EventInfo());
                SimpleJson.RegisterAotType(typeof(Octokit.Organization), () => new Octokit.Organization());
                SimpleJson.RegisterAotType(typeof(Octokit.Team), () => new Octokit.Team());                
                SimpleJson.RegisterAotType(typeof(Octokit.RateLimit), () => new Octokit.RateLimit());
                
                // API 错误响应
                SimpleJson.RegisterAotType(typeof(Octokit.ApiError), () => new Octokit.ApiError());
                SimpleJson.RegisterAotType(typeof(Octokit.ApiErrorDetail), () => new Octokit.ApiErrorDetail());
                
                // 认证相关
                SimpleJson.RegisterAotType(typeof(Octokit.AccessToken), () => new Octokit.AccessToken());
                SimpleJson.RegisterAotType(typeof(Octokit.Authorization), () => new Octokit.Authorization());
                SimpleJson.RegisterAotType(typeof(Octokit.ApplicationAuthorization), () => new Octokit.ApplicationAuthorization());
                SimpleJson.RegisterAotType(typeof(Octokit.OauthToken), () => new Octokit.OauthToken());
                SimpleJson.RegisterAotType(typeof(Octokit.OauthDeviceFlowResponse), () => new Octokit.OauthDeviceFlowResponse());
                
                // 评论
                SimpleJson.RegisterAotType(typeof(Octokit.IssueComment), () => new Octokit.IssueComment());
                SimpleJson.RegisterAotType(typeof(Octokit.CommitComment), () => new Octokit.CommitComment());
                SimpleJson.RegisterAotType(typeof(Octokit.PullRequestReviewComment), () => new Octokit.PullRequestReviewComment());
                SimpleJson.RegisterAotType(typeof(Octokit.GistComment), () => new Octokit.GistComment());
                
                // Check Run / Suite
                SimpleJson.RegisterAotType(typeof(Octokit.CheckRun), () => new Octokit.CheckRun());
                SimpleJson.RegisterAotType(typeof(Octokit.CheckSuite), () => new Octokit.CheckSuite());
                SimpleJson.RegisterAotType(typeof(Octokit.CheckRunAnnotation), () => new Octokit.CheckRunAnnotation());
                SimpleJson.RegisterAotType(typeof(Octokit.CheckRunOutputResponse), () => new Octokit.CheckRunOutputResponse());
                SimpleJson.RegisterAotType(typeof(Octokit.CheckRunRequestedAction), () => new Octokit.CheckRunRequestedAction());
                SimpleJson.RegisterAotType(typeof(Octokit.CheckRunsResponse), () => new Octokit.CheckRunsResponse());
                SimpleJson.RegisterAotType(typeof(Octokit.CheckSuitesResponse), () => new Octokit.CheckSuitesResponse());
                SimpleJson.RegisterAotType(typeof(Octokit.CheckSuitePreferencesResponse), () => new Octokit.CheckSuitePreferencesResponse());
                SimpleJson.RegisterAotType(typeof(Octokit.CombinedCommitStatus), () => new Octokit.CombinedCommitStatus());
                SimpleJson.RegisterAotType(typeof(Octokit.CommitStatus), () => new Octokit.CommitStatus());
                
                // 仓库内容
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryContent), () => new Octokit.RepositoryContent());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryContentInfo), () => new Octokit.RepositoryContentInfo());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryContentChangeSet), () => new Octokit.RepositoryContentChangeSet());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryContentLicense), () => new Octokit.RepositoryContentLicense());
                SimpleJson.RegisterAotType(typeof(Octokit.Readme), () => new Octokit.Readme());
                SimpleJson.RegisterAotType(typeof(Octokit.ReadmeResponse), () => new Octokit.ReadmeResponse());
                
                // 部署
                SimpleJson.RegisterAotType(typeof(Octokit.Deployment), () => new Octokit.Deployment());
                SimpleJson.RegisterAotType(typeof(Octokit.DeploymentStatus), () => new Octokit.DeploymentStatus());
                SimpleJson.RegisterAotType(typeof(Octokit.Models.Response.DeploymentEnvironment), () => new Octokit.Models.Response.DeploymentEnvironment());
                SimpleJson.RegisterAotType(typeof(Octokit.Models.Response.DeploymentEnvironmentsResponse), () => new Octokit.Models.Response.DeploymentEnvironmentsResponse());
                SimpleJson.RegisterAotType(typeof(Octokit.EnvironmentApproval), () => new Octokit.EnvironmentApproval());
                SimpleJson.RegisterAotType(typeof(Octokit.EnvironmentApprovals), () => new Octokit.EnvironmentApprovals());
                
                // 事件与通知
                SimpleJson.RegisterAotType(typeof(Octokit.IssueEvent), () => new Octokit.IssueEvent());
                SimpleJson.RegisterAotType(typeof(Octokit.Notification), () => new Octokit.Notification());
                SimpleJson.RegisterAotType(typeof(Octokit.NotificationInfo), () => new Octokit.NotificationInfo());
                SimpleJson.RegisterAotType(typeof(Octokit.TimelineEventInfo), () => new Octokit.TimelineEventInfo());
                
                // Gist
                SimpleJson.RegisterAotType(typeof(Octokit.Gist), () => new Octokit.Gist());
                SimpleJson.RegisterAotType(typeof(Octokit.GistFile), () => new Octokit.GistFile());
                SimpleJson.RegisterAotType(typeof(Octokit.GistFork), () => new Octokit.GistFork());
                SimpleJson.RegisterAotType(typeof(Octokit.GistHistory), () => new Octokit.GistHistory());
                SimpleJson.RegisterAotType(typeof(Octokit.GistChangeStatus), () => new Octokit.GistChangeStatus());
                
                // Git 对象
                SimpleJson.RegisterAotType(typeof(Octokit.GitReference), () => new Octokit.GitReference());
                SimpleJson.RegisterAotType(typeof(Octokit.GitHubCommit), () => new Octokit.GitHubCommit());
                SimpleJson.RegisterAotType(typeof(Octokit.GitHubCommitFile), () => new Octokit.GitHubCommitFile());
                SimpleJson.RegisterAotType(typeof(Octokit.GitHubCommitStats), () => new Octokit.GitHubCommitStats());
                SimpleJson.RegisterAotType(typeof(Octokit.Reference), () => new Octokit.Reference());
                SimpleJson.RegisterAotType(typeof(Octokit.Blob), () => new Octokit.Blob());
                SimpleJson.RegisterAotType(typeof(Octokit.BlobReference), () => new Octokit.BlobReference());
                SimpleJson.RegisterAotType(typeof(Octokit.TreeItem), () => new Octokit.TreeItem());
                SimpleJson.RegisterAotType(typeof(Octokit.TreeResponse), () => new Octokit.TreeResponse());
                SimpleJson.RegisterAotType(typeof(Octokit.TagObject), () => new Octokit.TagObject());
                SimpleJson.RegisterAotType(typeof(Octokit.GitTag), () => new Octokit.GitTag());
                SimpleJson.RegisterAotType(typeof(Octokit.Merge), () => new Octokit.Merge());
                SimpleJson.RegisterAotType(typeof(Octokit.CompareResult), () => new Octokit.CompareResult());
                
                // Issue 相关
                SimpleJson.RegisterAotType(typeof(Octokit.Reaction), () => new Octokit.Reaction());
                SimpleJson.RegisterAotType(typeof(Octokit.ReactionSummary), () => new Octokit.ReactionSummary());
                SimpleJson.RegisterAotType(typeof(Octokit.IssueEventProjectCard), () => new Octokit.IssueEventProjectCard());
                SimpleJson.RegisterAotType(typeof(Octokit.DismissedReviewInfo), () => new Octokit.DismissedReviewInfo());
                
                // 组织
                SimpleJson.RegisterAotType(typeof(Octokit.OrganizationMembership), () => new Octokit.OrganizationMembership());
                SimpleJson.RegisterAotType(typeof(Octokit.OrganizationMembershipInvitation), () => new Octokit.OrganizationMembershipInvitation());
                SimpleJson.RegisterAotType(typeof(Octokit.OrganizationHook), () => new Octokit.OrganizationHook());
                SimpleJson.RegisterAotType(typeof(Octokit.OrganizationCredential), () => new Octokit.OrganizationCredential());
                
                // PR 相关
                SimpleJson.RegisterAotType(typeof(Octokit.PullRequestReview), () => new Octokit.PullRequestReview());
                SimpleJson.RegisterAotType(typeof(Octokit.PullRequestFile), () => new Octokit.PullRequestFile());
                SimpleJson.RegisterAotType(typeof(Octokit.PullRequestCommit), () => new Octokit.PullRequestCommit());
                SimpleJson.RegisterAotType(typeof(Octokit.PullRequestMerge), () => new Octokit.PullRequestMerge());
                SimpleJson.RegisterAotType(typeof(Octokit.RequestedReviews), () => new Octokit.RequestedReviews());
                
                // Release
                SimpleJson.RegisterAotType(typeof(Octokit.ReleaseAsset), () => new Octokit.ReleaseAsset());
                SimpleJson.RegisterAotType(typeof(Octokit.GeneratedReleaseNotes), () => new Octokit.GeneratedReleaseNotes());
                
                // 搜索结果
                SimpleJson.RegisterAotType(typeof(Octokit.SearchCodeResult), () => new Octokit.SearchCodeResult());
                SimpleJson.RegisterAotType(typeof(Octokit.SearchRepositoryResult), () => new Octokit.SearchRepositoryResult());
                SimpleJson.RegisterAotType(typeof(Octokit.SearchIssuesResult), () => new Octokit.SearchIssuesResult());
                SimpleJson.RegisterAotType(typeof(Octokit.SearchUsersResult), () => new Octokit.SearchUsersResult());
                SimpleJson.RegisterAotType(typeof(Octokit.SearchLabelsResult), () => new Octokit.SearchLabelsResult());
                SimpleJson.RegisterAotType(typeof(Octokit.SearchCode), () => new Octokit.SearchCode());
                
                // 统计
                SimpleJson.RegisterAotType(typeof(Octokit.Contributor), () => new Octokit.Contributor());
                SimpleJson.RegisterAotType(typeof(Octokit.CodeFrequency), () => new Octokit.CodeFrequency());
                SimpleJson.RegisterAotType(typeof(Octokit.CommitActivity), () => new Octokit.CommitActivity());
                SimpleJson.RegisterAotType(typeof(Octokit.PunchCard), () => new Octokit.PunchCard());
                SimpleJson.RegisterAotType(typeof(Octokit.Participation), () => new Octokit.Participation());
                SimpleJson.RegisterAotType(typeof(Octokit.WeeklyHash), () => new Octokit.WeeklyHash());
                SimpleJson.RegisterAotType(typeof(Octokit.WeeklyCommitActivity), () => new Octokit.WeeklyCommitActivity());
                SimpleJson.RegisterAotType(typeof(Octokit.AdditionsAndDeletions), () => new Octokit.AdditionsAndDeletions());
                
                // 团队
                SimpleJson.RegisterAotType(typeof(Octokit.TeamMembershipDetails), () => new Octokit.TeamMembershipDetails());
                SimpleJson.RegisterAotType(typeof(Octokit.TeamRepository), () => new Octokit.TeamRepository());
                
                // 用户
                SimpleJson.RegisterAotType(typeof(Octokit.EmailAddress), () => new Octokit.EmailAddress());
                SimpleJson.RegisterAotType(typeof(Octokit.GpgKey), () => new Octokit.GpgKey());
                SimpleJson.RegisterAotType(typeof(Octokit.PublicKey), () => new Octokit.PublicKey());
                SimpleJson.RegisterAotType(typeof(Octokit.Plan), () => new Octokit.Plan());
                SimpleJson.RegisterAotType(typeof(Octokit.UserRenameResponse), () => new Octokit.UserRenameResponse());
                SimpleJson.RegisterAotType(typeof(Octokit.UserStar), () => new Octokit.UserStar());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryStar), () => new Octokit.RepositoryStar());
                
                // 工作流
                SimpleJson.RegisterAotType(typeof(Octokit.Workflow), () => new Octokit.Workflow());
                SimpleJson.RegisterAotType(typeof(Octokit.WorkflowRun), () => new Octokit.WorkflowRun());
                SimpleJson.RegisterAotType(typeof(Octokit.WorkflowJob), () => new Octokit.WorkflowJob());
                SimpleJson.RegisterAotType(typeof(Octokit.WorkflowJobStep), () => new Octokit.WorkflowJobStep());
                SimpleJson.RegisterAotType(typeof(Octokit.WorkflowReference), () => new Octokit.WorkflowReference());
                SimpleJson.RegisterAotType(typeof(Octokit.WorkflowRunTiming), () => new Octokit.WorkflowRunTiming());
                SimpleJson.RegisterAotType(typeof(Octokit.WorkflowRunUsage), () => new Octokit.WorkflowRunUsage());
                SimpleJson.RegisterAotType(typeof(Octokit.WorkflowUsage), () => new Octokit.WorkflowUsage());
                SimpleJson.RegisterAotType(typeof(Octokit.WorkflowBillable), () => new Octokit.WorkflowBillable());
                SimpleJson.RegisterAotType(typeof(Octokit.WorkflowRunBillable), () => new Octokit.WorkflowRunBillable());
                SimpleJson.RegisterAotType(typeof(Octokit.WorkflowsResponse), () => new Octokit.WorkflowsResponse());
                SimpleJson.RegisterAotType(typeof(Octokit.WorkflowRunsResponse), () => new Octokit.WorkflowRunsResponse());
                SimpleJson.RegisterAotType(typeof(Octokit.WorkflowJobsResponse), () => new Octokit.WorkflowJobsResponse());
                
                // 速率限制
                SimpleJson.RegisterAotType(typeof(Octokit.ResourceRateLimit), () => new Octokit.ResourceRateLimit());
                SimpleJson.RegisterAotType(typeof(Octokit.MiscellaneousRateLimit), () => new Octokit.MiscellaneousRateLimit());
                
                // 仓库相关
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryPermissions), () => new Octokit.RepositoryPermissions());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryHook), () => new Octokit.RepositoryHook());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryInvitation), () => new Octokit.RepositoryInvitation());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryTag), () => new Octokit.RepositoryTag());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryTopics), () => new Octokit.RepositoryTopics());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryLanguage), () => new Octokit.RepositoryLanguage());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryContributor), () => new Octokit.RepositoryContributor());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoriesResponse), () => new Octokit.RepositoriesResponse());
                SimpleJson.RegisterAotType(typeof(Octokit.DeployKey), () => new Octokit.DeployKey());
                SimpleJson.RegisterAotType(typeof(Octokit.CollaboratorPermission), () => new Octokit.CollaboratorPermission());
                SimpleJson.RegisterAotType(typeof(Octokit.RenameInfo), () => new Octokit.RenameInfo());
                SimpleJson.RegisterAotType(typeof(Octokit.SourceInfo), () => new Octokit.SourceInfo());
                SimpleJson.RegisterAotType(typeof(Octokit.Verification), () => new Octokit.Verification());
                SimpleJson.RegisterAotType(typeof(Octokit.License), () => new Octokit.License());
                SimpleJson.RegisterAotType(typeof(Octokit.LicenseMetadata), () => new Octokit.LicenseMetadata());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositorySecret), () => new Octokit.RepositorySecret());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositorySecretsCollection), () => new Octokit.RepositorySecretsCollection());
                SimpleJson.RegisterAotType(typeof(Octokit.SecretsPublicKey), () => new Octokit.SecretsPublicKey());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryCreatedEvent), () => new Octokit.RepositoryCreatedEvent());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryVisibilityChangeEvent), () => new Octokit.RepositoryVisibilityChangeEvent());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryCodeOwnersErrors), () => new Octokit.RepositoryCodeOwnersErrors());
                
                // 分支保护
                SimpleJson.RegisterAotType(typeof(Octokit.BranchProtectionSettings), () => new Octokit.BranchProtectionSettings());
                SimpleJson.RegisterAotType(typeof(Octokit.BranchProtectionRule), () => new Octokit.BranchProtectionRule());
                SimpleJson.RegisterAotType(typeof(Octokit.EnforceAdmins), () => new Octokit.EnforceAdmins());
                SimpleJson.RegisterAotType(typeof(Octokit.BranchProtectionRequiredStatusChecks), () => new Octokit.BranchProtectionRequiredStatusChecks());
                SimpleJson.RegisterAotType(typeof(Octokit.BranchProtectionPushRestrictions), () => new Octokit.BranchProtectionPushRestrictions());
                SimpleJson.RegisterAotType(typeof(Octokit.BranchProtectionRequiredReviews), () => new Octokit.BranchProtectionRequiredReviews());
                
                // 安装
                SimpleJson.RegisterAotType(typeof(Octokit.Installation), () => new Octokit.Installation());
                SimpleJson.RegisterAotType(typeof(Octokit.InstallationId), () => new Octokit.InstallationId());
                SimpleJson.RegisterAotType(typeof(Octokit.InstallationPermissions), () => new Octokit.InstallationPermissions());
                SimpleJson.RegisterAotType(typeof(Octokit.InstallationsResponse), () => new Octokit.InstallationsResponse());
                SimpleJson.RegisterAotType(typeof(Octokit.GitHubApp), () => new Octokit.GitHubApp());
                
                // 其他
                SimpleJson.RegisterAotType(typeof(Octokit.Author), () => new Octokit.Author());
                // Account 是抽象类，不能直接实例化，跳过注册
                SimpleJson.RegisterAotType(typeof(Octokit.Application), () => new Octokit.Application());
                SimpleJson.RegisterAotType(typeof(Octokit.AuditLogEvent), () => new Octokit.AuditLogEvent());
                SimpleJson.RegisterAotType(typeof(Octokit.Emoji), () => new Octokit.Emoji());
                SimpleJson.RegisterAotType(typeof(Octokit.Feed), () => new Octokit.Feed());
                SimpleJson.RegisterAotType(typeof(Octokit.FeedLink), () => new Octokit.FeedLink());
                SimpleJson.RegisterAotType(typeof(Octokit.FeedLinks), () => new Octokit.FeedLinks());
                SimpleJson.RegisterAotType(typeof(Octokit.GitIgnoreTemplate), () => new Octokit.GitIgnoreTemplate());
                SimpleJson.RegisterAotType(typeof(Octokit.Meta), () => new Octokit.Meta());
                SimpleJson.RegisterAotType(typeof(Octokit.Migration), () => new Octokit.Migration());
                SimpleJson.RegisterAotType(typeof(Octokit.Page), () => new Octokit.Page());
                SimpleJson.RegisterAotType(typeof(Octokit.PagesBuild), () => new Octokit.PagesBuild());
                SimpleJson.RegisterAotType(typeof(Octokit.Project), () => new Octokit.Project());
                SimpleJson.RegisterAotType(typeof(Octokit.ProjectCard), () => new Octokit.ProjectCard());
                SimpleJson.RegisterAotType(typeof(Octokit.ProjectColumn), () => new Octokit.ProjectColumn());
                SimpleJson.RegisterAotType(typeof(Octokit.Subscription), () => new Octokit.Subscription());
                SimpleJson.RegisterAotType(typeof(Octokit.ThreadSubscription), () => new Octokit.ThreadSubscription());
                SimpleJson.RegisterAotType(typeof(Octokit.Package), () => new Octokit.Package());
                SimpleJson.RegisterAotType(typeof(Octokit.PackageVersion), () => new Octokit.PackageVersion());
                SimpleJson.RegisterAotType(typeof(Octokit.CommitPullRequest), () => new Octokit.CommitPullRequest());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryTrafficPath), () => new Octokit.RepositoryTrafficPath());
                SimpleJson.RegisterAotType(typeof(Octokit.RepositoryTrafficReferrer), () => new Octokit.RepositoryTrafficReferrer());
                SimpleJson.RegisterAotType(typeof(Octokit.OrganizationSecret), () => new Octokit.OrganizationSecret());
                SimpleJson.RegisterAotType(typeof(Octokit.OrganizationSecretsCollection), () => new Octokit.OrganizationSecretsCollection());
                SimpleJson.RegisterAotType(typeof(Octokit.Copilot.CopilotSeat), () => new Octokit.Copilot.CopilotSeat());
                SimpleJson.RegisterAotType(typeof(Octokit.Copilot.CopilotSeatsResponse), () => new Octokit.Copilot.CopilotSeatsResponse());
                
                // Webhook Payload
                SimpleJson.RegisterAotType(typeof(Octokit.PushWebhookPayload), () => new Octokit.PushWebhookPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.PushWebhookCommit), () => new Octokit.PushWebhookCommit());
                SimpleJson.RegisterAotType(typeof(Octokit.PushWebhookCommitter), () => new Octokit.PushWebhookCommitter());
                SimpleJson.RegisterAotType(typeof(Octokit.InstallationEventPayload), () => new Octokit.InstallationEventPayload());
                SimpleJson.RegisterAotType(typeof(Octokit.ActivityWithActionPayload), () => new Octokit.ActivityWithActionPayload());
                
                // 注册枚举类型          
                SimpleJson.RegisterAotType(typeof(Octokit.Committer), () =>new Octokit.Committer());              
                SimpleJson.RegisterAotType(typeof(Octokit.CheckSuitePreferenceAutoTrigger), () =>new Octokit.CheckSuitePreferenceAutoTrigger());
                SimpleJson.RegisterAotType(typeof(Octokit.CheckSuitePreferences), () => new Octokit.CheckSuitePreferences());

                // 注册 List<T> 集合类型（AOT 环境下 MakeGenericType 不可用，必须预注册）
                RegisterListTypes();
            }
            catch
            {
                // 忽略注册失败
            }
        }

        private static void RegisterListTypes()
        {
            // 基元类型 List
            SimpleJson.RegisterAotType(typeof(List<string>), () => new List<string>());
            SimpleJson.RegisterAotType(typeof(List<int>), () => new List<int>());
            SimpleJson.RegisterAotType(typeof(List<long>), () => new List<long>());

            // Octokit 模型 List
            SimpleJson.RegisterAotType(typeof(List<Octokit.Repository>), () => new List<Octokit.Repository>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.User>), () => new List<Octokit.User>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Issue>), () => new List<Octokit.Issue>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.PullRequest>), () => new List<Octokit.PullRequest>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Branch>), () => new List<Octokit.Branch>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Commit>), () => new List<Octokit.Commit>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Label>), () => new List<Octokit.Label>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Milestone>), () => new List<Octokit.Milestone>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Team>), () => new List<Octokit.Team>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Organization>), () => new List<Octokit.Organization>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Release>), () => new List<Octokit.Release>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.ReleaseAsset>), () => new List<Octokit.ReleaseAsset>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Activity>), () => new List<Octokit.Activity>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.EventInfo>), () => new List<Octokit.EventInfo>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.IssueComment>), () => new List<Octokit.IssueComment>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.CommitComment>), () => new List<Octokit.CommitComment>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.PullRequestReviewComment>), () => new List<Octokit.PullRequestReviewComment>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.GistComment>), () => new List<Octokit.GistComment>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.CheckRun>), () => new List<Octokit.CheckRun>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.CheckSuite>), () => new List<Octokit.CheckSuite>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.CheckRunAnnotation>), () => new List<Octokit.CheckRunAnnotation>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.CommitStatus>), () => new List<Octokit.CommitStatus>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Deployment>), () => new List<Octokit.Deployment>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.DeploymentStatus>), () => new List<Octokit.DeploymentStatus>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Models.Response.DeploymentEnvironment>), () => new List<Octokit.Models.Response.DeploymentEnvironment>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.EnvironmentApproval>), () => new List<Octokit.EnvironmentApproval>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.IssueEvent>), () => new List<Octokit.IssueEvent>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Notification>), () => new List<Octokit.Notification>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Gist>), () => new List<Octokit.Gist>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.GistFile>), () => new List<Octokit.GistFile>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.GistFork>), () => new List<Octokit.GistFork>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.GistHistory>), () => new List<Octokit.GistHistory>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.GitReference>), () => new List<Octokit.GitReference>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.GitHubCommit>), () => new List<Octokit.GitHubCommit>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.GitHubCommitFile>), () => new List<Octokit.GitHubCommitFile>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Reference>), () => new List<Octokit.Reference>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.TreeItem>), () => new List<Octokit.TreeItem>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.RepositoryContent>), () => new List<Octokit.RepositoryContent>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.RepositoryHook>), () => new List<Octokit.RepositoryHook>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.RepositoryTag>), () => new List<Octokit.RepositoryTag>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.RepositoryContributor>), () => new List<Octokit.RepositoryContributor>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.RepositoryInvitation>), () => new List<Octokit.RepositoryInvitation>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.DeployKey>), () => new List<Octokit.DeployKey>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Reaction>), () => new List<Octokit.Reaction>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.PullRequestReview>), () => new List<Octokit.PullRequestReview>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.PullRequestFile>), () => new List<Octokit.PullRequestFile>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.PullRequestCommit>), () => new List<Octokit.PullRequestCommit>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.EmailAddress>), () => new List<Octokit.EmailAddress>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.GpgKey>), () => new List<Octokit.GpgKey>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.PublicKey>), () => new List<Octokit.PublicKey>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Workflow>), () => new List<Octokit.Workflow>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.WorkflowRun>), () => new List<Octokit.WorkflowRun>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.WorkflowJob>), () => new List<Octokit.WorkflowJob>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.WorkflowJobStep>), () => new List<Octokit.WorkflowJobStep>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.WorkflowReference>), () => new List<Octokit.WorkflowReference>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.WorkflowRunTiming>), () => new List<Octokit.WorkflowRunTiming>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.WeeklyCommitActivity>), () => new List<Octokit.WeeklyCommitActivity>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.WeeklyHash>), () => new List<Octokit.WeeklyHash>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.AdditionsAndDeletions>), () => new List<Octokit.AdditionsAndDeletions>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.PunchCardPoint>), () => new List<Octokit.PunchCardPoint>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Contributor>), () => new List<Octokit.Contributor>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.SearchCode>), () => new List<Octokit.SearchCode>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.ApiErrorDetail>), () => new List<Octokit.ApiErrorDetail>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Installation>), () => new List<Octokit.Installation>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.OrganizationMembership>), () => new List<Octokit.OrganizationMembership>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.OrganizationHook>), () => new List<Octokit.OrganizationHook>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.OrganizationSecret>), () => new List<Octokit.OrganizationSecret>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.RepositorySecret>), () => new List<Octokit.RepositorySecret>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Copilot.CopilotSeat>), () => new List<Octokit.Copilot.CopilotSeat>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.PushWebhookCommit>), () => new List<Octokit.PushWebhookCommit>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.RepositoryCodeOwnersErrors.RepositoryCodeOwnersError>), () => new List<Octokit.RepositoryCodeOwnersErrors.RepositoryCodeOwnersError>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.RepositoryTrafficClone>), () => new List<Octokit.RepositoryTrafficClone>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.RepositoryTrafficView>), () => new List<Octokit.RepositoryTrafficView>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Project>), () => new List<Octokit.Project>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.ProjectCard>), () => new List<Octokit.ProjectCard>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.ProjectColumn>), () => new List<Octokit.ProjectColumn>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Migration>), () => new List<Octokit.Migration>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Page>), () => new List<Octokit.Page>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.PagesBuild>), () => new List<Octokit.PagesBuild>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.Package>), () => new List<Octokit.Package>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.PackageVersion>), () => new List<Octokit.PackageVersion>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.CommitPullRequest>), () => new List<Octokit.CommitPullRequest>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.AuditLogEvent>), () => new List<Octokit.AuditLogEvent>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.BranchProtectionRule>), () => new List<Octokit.BranchProtectionRule>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.TeamMembershipDetails>), () => new List<Octokit.TeamMembershipDetails>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.TeamRepository>), () => new List<Octokit.TeamRepository>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.CollaboratorPermission>), () => new List<Octokit.CollaboratorPermission>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.RequestedReviews>), () => new List<Octokit.RequestedReviews>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.DismissedReviewInfo>), () => new List<Octokit.DismissedReviewInfo>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.IssueEventProjectCard>), () => new List<Octokit.IssueEventProjectCard>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.CheckSuitePreferenceAutoTrigger>), () => new List<Octokit.CheckSuitePreferenceAutoTrigger>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.RepositoryTrafficPath>), () => new List<Octokit.RepositoryTrafficPath>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.RepositoryTrafficReferrer>), () => new List<Octokit.RepositoryTrafficReferrer>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.MaintenanceModeActiveProcesses>), () => new List<Octokit.MaintenanceModeActiveProcesses>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.CheckRunRequestedAction>), () => new List<Octokit.CheckRunRequestedAction>());
            SimpleJson.RegisterAotType(typeof(List<Octokit.CheckRunOutputResponse>), () => new List<Octokit.CheckRunOutputResponse>());
        }
        static void ResisterEnumTypes() {
            // 标记枚举类型防止 AOT 裁剪，确保枚举值在 AOT 编译中可用
            var e1 = Octokit.ContentType.Dir;
            var e2 = Octokit.LockReason.OffTopic;
            var e3 = Octokit.PackageVisibility.Public;
            var e4 = Octokit.PackageVersionState.Deleted;
            var e5 = Octokit.PackageType.RubyGems;
            var e6 = Octokit.ItemState.Open;
            var e7 = Octokit.ItemStateFilter.All;
            var e8 = Octokit.RepositoryType.All;
            var e9 = Octokit.RepositoryVisibility.Public;
            var e10 = Octokit.SortDirection.Ascending;
            var e11 = Octokit.PullRequestReviewState.Approved;
            var e12 = Octokit.MergeableState.Clean;
            var e13 = Octokit.CommitState.Success;
            var e14 = Octokit.AccountType.User;
            var e15 = Octokit.AuthorAssociation.Owner;
            var e16 = Octokit.CheckStatus.Completed;
            var e17 = Octokit.CheckConclusion.Success;
            var e18 = Octokit.CheckAnnotationLevel.Notice;
            var e19 = Octokit.DeploymentState.Success;
            var e20 = Octokit.PermissionLevel.Admin;
            var e21 = Octokit.Permission.Admin;
            var e22 = Octokit.TeamPrivacy.Secret;
            var e23 = Octokit.TeamRole.Member;
            var e24 = Octokit.MembershipState.Active;
            var e25 = Octokit.OrganizationMembershipRole.Admin;
            var e26 = Octokit.EventInfoState.Closed;
            var e27 = Octokit.RefType.Branch;
            var e28 = Octokit.TreeType.Blob;
            var e29 = Octokit.EncodingType.Base64;
            var e30 = Octokit.VerificationReason.Valid;
            var e31 = Octokit.WorkflowRunConclusion.Success;
            var e32 = Octokit.WorkflowRunStatus.Completed;
            var e33 = Octokit.WorkflowJobConclusion.Success;
            var e34 = Octokit.WorkflowJobStatus.Completed;
            var e35 = Octokit.WorkflowState.Active;
            var e36 = Octokit.ReactionType.Plus1;
            var e37 = Octokit.IssueFilter.All;
            var e38 = Octokit.IssueSort.Created;
            var e39 = Octokit.PullRequestSort.Created;
            var e40 = Octokit.RepositorySort.Created;
            var e41 = Octokit.RepositoryAffiliation.All;
            var e42 = Octokit.CollaboratorAffiliation.All;
            var e43 = Octokit.MilestoneSort.DueDate;
            var e44 = Octokit.StarredSort.Created;
            var e45 = Octokit.Migration.MigrationState.Exported;
            var e46 = Octokit.InstallationRepositorySelection.Selected;
            var e47 = Octokit.EmailVisibility.Public;
            var e48 = Octokit.InvitationPermissionType.Write;
            var e49 = Octokit.TaggedType.Commit;
            var e50 = Octokit.TrafficDayOrWeek.Week;
            var e51 = Octokit.PagesBuildStatus.Built;
            var e52 = Octokit.MaintenanceModeStatus.Off;
            var e53 = Octokit.PullRequestMergeMethod.Merge;
            var e54 = Octokit.PullRequestReviewEvent.Approve;
            var e55 = Octokit.ProjectCardContentType.Issue;
            var e56 = Octokit.PendingDeploymentReviewState.Approved;
            var e57 = Octokit.TeamPermission.Pull;
            var e58 = Octokit.ProjectCardArchivedStateFilter.NotArchived;
            var e59 = Octokit.Sort.Newest;
            var e60 = Octokit.RepositoryRequestVisibility.Public;
            var e61 = Octokit.ArchiveFormat.Tarball;
            var e62 = Octokit.OrganizationMembersFilter.All;
            var e63 = Octokit.OrganizationMembersRole.Member;
            var e64 = Octokit.MembershipRole.Member;
            var e65 = Octokit.ItemStateReason.Completed;
            var e66 = Octokit.CheckStatusFilter.Completed;
            var e67 = Octokit.CheckRunCompletedAtFilter.Latest;
            var e68 = Octokit.CheckRunStatusFilter.Completed;
            var e69 = Octokit.CheckWarningLevel.Notice;
            var e70 = Octokit.EnvironmentApprovalState.Approved;
            var e71 = Octokit.InstallationPermissionLevel.Read;
            var e72 = Octokit.IssueCommentSort.Created;
            var e73 = Octokit.PullRequestReviewCommentSort.Created;
            var e74 = Octokit.PreReceiveEnvironmentDownloadState.Success;
            var e75 = Octokit.PreReceiveHookEnforcement.Enabled;
            var e76 = Octokit.WorkflowRunJobsFilter.All;
            var e77 = Octokit.TeamRoleFilter.Member;
            var e78 = Octokit.CodeSearchSort.Indexed;
            var e79 = Octokit.IssueSearchSort.Updated;
            var e80 = Octokit.IssueTypeQualifier.Issue;
            var e81 = Octokit.IssueInQualifier.Title;
            var e82 = Octokit.IssueIsQualifier.Open;
            var e83 = Octokit.IssueNoMetadataQualifier.Label;
            var e84 = Octokit.LabelSearchSort.Created;
            var e85 = Octokit.InQualifier.Name;
            var e86 = Octokit.ForkQualifier.OnlyForks;
            var e87 = Octokit.AccountSearchType.User;
            var e88 = Octokit.UserInQualifier.Username;
            var e89 = Octokit.UsersSearchSort.Followers;
            var e90 = Octokit.RepoSearchSort.Stars;
            var e91 = Octokit.Language.CSharp;
            var e92 = Octokit.RepoSearchLicense.MIT;
            var e93 = Octokit.SearchQualifierOperator.GreaterThan;
        }
        public string Serialize(object item)
        {
            return RS.SimpleJsonUnity.SimpleJson.SerializeObject(item,_serializationStrategy);
        }

        public T Deserialize<T>(string json)
        {
            return RS.SimpleJsonUnity.SimpleJson.DeserializeObject<T>(json,_serializationStrategy);
        }

        internal static string SerializeEnum(Enum value)
        {
            return value.ToParameter().ToString();
        }

        internal static object DeserializeEnum(string value,Type type)
        {
            return Enum.Parse(type,value,ignoreCase: true);
        }


    }

#endif
}
