using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace StdUnit.Tags.Tests.Core.TagsProjectCtrls;

/// <summary>
/// 模拟的测点项目工厂，用于 TagsProjectCtrl 单元测试。
/// </summary>
internal class MockTagsProjectFactory : ITagsProjectFactory
{
    private readonly List<MockTagsProject> _createdProjects = new();

    /// <summary>
    /// 最近一次创建的 <see cref="MockTagsProject"/>。
    /// </summary>
    public MockTagsProject? LastCreatedProject => _createdProjects.LastOrDefault();

    /// <summary>
    /// 所有已创建的 MockTagsProject 列表。
    /// </summary>
    public IReadOnlyList<MockTagsProject> CreatedProjects => _createdProjects;

    /// <summary>
    /// 所有已创建项目的 DisposeCallCount 总和。
    /// </summary>
    public int TotalDisposeCallCount => _createdProjects.Sum(p => p.DisposeCallCount);

    /// <summary>
    /// 如果为 true，<see cref="Create"/> 会抛出 InvalidOperationException。
    /// </summary>
    public bool CreateThrows { get; set; }

    public string? CapturedProjRoot { get; private set; }
    public XElement? CapturedRoot { get; private set; }

    public ITagsProject Create(string projRoot, XElement? root = null)
    {
        CapturedProjRoot = projRoot;
        CapturedRoot = root;

        if (CreateThrows)
        {
            throw new InvalidOperationException("模拟的工厂异常");
        }

        var project = new MockTagsProject();
        project.Initialize(projRoot, root);
        _createdProjects.Add(project);
        return project;
    }
}
