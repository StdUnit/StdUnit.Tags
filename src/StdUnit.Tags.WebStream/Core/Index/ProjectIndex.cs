namespace StdUnit.Tags.WebStream.Internal;

/// <summary>
/// 一个 epoch 内的项目索引：扁平的结构树 + 每个入口的观测视图。<br/>
/// 构建一次（项目启动时），之后只读。
/// </summary>
internal sealed class ProjectIndex
{
    private ProjectIndex(long epoch, TreeDto tree, List<EntryStream> entries)
    {
        this.Epoch = epoch;
        this.Tree = tree;
        this.Entries = entries;

        var byKey = new Dictionary<string, EntryStream>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            byKey[entry.Key] = entry;
        }
        this.ByKey = byKey;
    }

    public long Epoch { get; }

    public TreeDto Tree { get; }

    public IReadOnlyList<EntryStream> Entries { get; }

    public IReadOnlyDictionary<string, EntryStream> ByKey { get; }

    /// <summary>
    /// 从项目中构建索引。<b>O(节点数)，一次性</b>：在项目启动时（轮询开始之前）调用。<br/>
    /// <b>只遍历一次</b>：结构树与"每个入口的叶子/容器"在同一趟 DFS 里同时产出
    /// （入口子树两两不相交，所以把"当前入口的收集器"往下传就够了，不需要第二趟）。
    /// </summary>
    public static ProjectIndex Build(long epoch, ITagsProject project)
    {
        var root = project.Tags;
        var entryGroups = root.ScanEntries();

        var visitor = new IndexVisitor(entryGroups);
        new TagUnion.TagGrp(root).Accept(visitor);

        var entries = new List<EntryStream>(entryGroups.Count);
        foreach (var entry in entryGroups)
        {
            if (!visitor.Collected.TryGetValue(entry, out var accumulator))
            {
                continue;
            }

            entries.Add(new EntryStream(
                entry.TagName(),
                accumulator.Path,
                entry,
                accumulator.Leaves,
                accumulator.Containers
            ));
        }

        var tree = new TreeDto(epoch, true, root.TagName(), visitor.Nodes);
        return new ProjectIndex(epoch, tree, entries);
    }

    /// <summary>
    /// 一趟深度优先遍历的访问者：产出结构树节点，并在进入入口子树时把它的叶子与容器累积下来。<br/>
    /// <b>不继承 <see cref="TraversingVisitorBase"/></b>：基类的 <c>Process</c> 之后不会再通知子类"子树已结束"，
    /// 而这里必须知道何时退出子树（恢复父路径 / 深度 / 当前入口），所以自己维护进出栈的上下文
    /// （<see cref="Scope"/>）。遍历顺序与基类一致：先序、文档顺序。
    /// </summary>
    private sealed class IndexVisitor : ITagUnionVisitor
    {
        private readonly HashSet<ITagGrp> _entrySet;

        private string? _parentPath;
        private int _depth;

        /// <summary>"当前所在的入口累加器"；不在任何入口子树内时为 <c>null</c>（那部分节点只有结构、没有值）。</summary>
        private EntryAccumulator? _entry;

        public IndexVisitor(IList<ITagGrp> entryGroups)
        {
            // 只有 ScanEntries 认定的"最外层入口"才算入口（嵌套的 isEntry 不生效，见 NestedEntryValidator）。
            this._entrySet = new HashSet<ITagGrp>(entryGroups, ReferenceEqualityComparer.Instance);
        }

        public List<TagNodeDto> Nodes { get; } = new();

        public Dictionary<ITagGrp, EntryAccumulator> Collected { get; } = new(ReferenceEqualityComparer.Instance);

        /// <inheritdoc/>
        public void Visit(TagUnion.TagGrp grp)
        {
            var node = grp.Value;
            var scope = this.Enter(node.TagName());

            var isEntry = this._entrySet.Contains(node);
            if (isEntry)
            {
                this._entry = new EntryAccumulator(scope.Path);
                this.Collected[node] = this._entry;
            }

            this.Nodes.Add(new TagGrpNodeDto(
                Path: scope.Path,
                Parent: scope.ParentPath,
                Depth: scope.Depth,
                Name: node.TagName(),
                ChannelName: node.SearchChannel()?.ChannelName(),
                AccessMode: node.SearchAccessMode().ToString(),
                EntryKey: isEntry ? node.TagName() : null,
                IsEntry: node.Descriptor.IsEntry,
                IsEnabled: node.IsEnabled,
                ScanIntervalMs: node.SearchScanInterval()));

            this._entry?.Containers.Add(new ContainerNode(scope.Path, node, null));

            foreach (var child in node.Children.Values)
            {
                child.Accept(this);
            }

            this.Leave(scope);
        }

        /// <inheritdoc/>
        public void Visit(TagUnion.TagCbnt cbnt)
        {
            var node = cbnt.Value;
            var scope = this.Enter(node.TagName());

            this.Nodes.Add(new TagCbntNodeDto(
                Path: scope.Path,
                Parent: scope.ParentPath,
                Depth: scope.Depth,
                Name: node.TagName(),
                ChannelName: node.SearchChannel()?.ChannelName(),
                AccessMode: node.SearchAccessMode().ToString(),
                IsEnabled: node.IsEnabled,
                StartAddress: node.StartAddress));

            this._entry?.Containers.Add(new ContainerNode(scope.Path, null, node));

            // 组合的子节点只可能是叶测点，直接走 VisitTag，省掉一层 TagUnit 包装。
            foreach (var child in node.Children.Values)
            {
                this.VisitTag(child);
            }

            this.Leave(scope);
        }

        /// <inheritdoc/>
        public void Visit(TagUnion.TagUnit tag) => this.VisitTag(tag.Value);

        private void VisitTag(ITag tag)
        {
            var path = Combine(this._parentPath, tag.TagName());

            this.Nodes.Add(new TagLeafNodeDto(
                Path: path,
                Parent: this._parentPath,
                Depth: this._depth,
                Name: tag.TagName(),
                ChannelName: tag.SearchChannel()?.ChannelName(),
                AccessMode: tag.SearchAccessMode().ToString(),
                TagKind: tag.TagKind(),
                Address: tag.NormalizedAddress(),
                TagSize: tag.TagSize(),
                IsReadOnly: tag.IsReadOnly()));

            this._entry?.Leaves.Add(new LeafTag(path, tag));
        }

        /// <summary>进入一个容器节点：算出它自己的路径/深度，并把上下文切到"它的子树"。</summary>
        private Scope Enter(string name)
        {
            var scope = new Scope(Combine(this._parentPath, name), this._parentPath, this._depth, this._entry);
            this._parentPath = scope.Path;
            this._depth = scope.Depth + 1;
            return scope;
        }

        /// <summary>离开容器节点：恢复进入前的父路径 / 深度 / 当前入口。</summary>
        private void Leave(Scope scope)
        {
            this._parentPath = scope.ParentPath;
            this._depth = scope.Depth;
            this._entry = scope.PreviousEntry;
        }

        private static string Combine(string? parentPath, string name)
        {
            if (parentPath is null)
            {
                // 根节点不占路径段：其 Path 为空串，子节点直接用名字。
                return string.Empty;
            }

            return parentPath.Length == 0 ? name : parentPath + "/" + name;
        }

        /// <summary>一次 <see cref="Enter"/> 的上下文快照：本节点的路径/父路径/深度，以及进入前的入口累加器。</summary>
        private readonly struct Scope
        {
            public Scope(string path, string? parentPath, int depth, EntryAccumulator? previousEntry)
            {
                this.Path = path;
                this.ParentPath = parentPath;
                this.Depth = depth;
                this.PreviousEntry = previousEntry;
            }

            /// <summary>本节点的路径。</summary>
            public string Path { get; }

            /// <summary>本节点的父路径（也就是进入前的当前路径）。</summary>
            public string? ParentPath { get; }

            /// <summary>本节点的深度。</summary>
            public int Depth { get; }

            /// <summary>进入本节点之前的入口累加器，离开时恢复。</summary>
            public EntryAccumulator? PreviousEntry { get; }
        }
    }

    /// <summary>单趟遍历时，为每个入口顺带累积它的叶子与容器。</summary>
    private sealed class EntryAccumulator
    {
        public EntryAccumulator(string path) => this.Path = path;

        public string Path { get; }

        public List<LeafTag> Leaves { get; } = new();

        public List<ContainerNode> Containers { get; } = new();
    }
}
