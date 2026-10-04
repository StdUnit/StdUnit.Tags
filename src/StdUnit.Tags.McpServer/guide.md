

StdUnit.Tags是一套面向工业场景的测点通信库。测点项目都会有一个静态结构信息(用XML表示)，其中描述了各个通道、层级式的测点点位等信息。
这个静态结构信息是后续所有点位读写的基础上下文。

### 示例

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>

	<!-- 通道，可以配置多个-->
	<Channel name="S7-1" driver="S7" >
		<IpAddr>localhost</IpAddr>
		<Rack>0</Rack>
		<Slot>1</Slot>
	</Channel>

	<!-- 测点配置，可以配置多个 -->
	<TagGrp name="CallMat" isEntry="true" isEnabled="true" channel="S7-1" scanInterval="10">

		<TagGrp name="1#">
			<TagGrp name="送料">
				<TagCbnt name="PLC" address="DB1990.0">
					<Tag name="叫料请求" address="$$10.0" type="BIT" access="RO"></Tag>
					<Tag name="进站响应" address="$$10.1" type="BIT" access="RO"></Tag>
					<Tag name="超时响应" address="$$10.2" type="BIT" access="RO"></Tag>

					<Tag name="叫料序号" address="$$12" type="INT32" access="RO" endian="BigEndian"></Tag>
				</TagCbnt>
				<TagCbnt name="MST" address="DB1991.0" access="RW">
					<Tag name="叫料应答" address="$$10.0" type="BIT"></Tag>
					<Tag name="进站请求" address="$$10.1" type="BIT"></Tag>
					<Tag name="超时请求" address="$$10.2" type="BIT"></Tag>

					<Tag name="状态" address="$$12" type="INT16" endian="BigEndian"></Tag>
					<Tag name="NG原因" address="$$14" type="INT16" endian="BigEndian"></Tag>

					<Tag name="叫料编号" address="$$16" type="STR" maxlen="24"></Tag>
				</TagCbnt>
			</TagGrp>
			
			<TagGrp name="取泡棉">
				<TagCbnt name="PLC" address="DB1990.0">
					<Tag name="叫料请求" address="$$30.0" type="BIT" access="RO"></Tag>
					<Tag name="进站响应" address="$$30.1" type="BIT" access="RO"></Tag>
					<Tag name="超时响应" address="$$30.2" type="BIT" access="RO"></Tag>

					<Tag name="叫料序号" address="$$32" type="INT32" access="RO" endian="BigEndian"></Tag>
				</TagCbnt>
				<TagCbnt name="MST" address="DB1991.0" access="RW">
					<Tag name="叫料应答" address="$$60.0" type="BIT"></Tag>
					<Tag name="进站请求" address="$$60.1" type="BIT"></Tag>
					<Tag name="超时请求" address="$$60.2" type="BIT"></Tag>

					<Tag name="状态" address="$$62" type="INT16" endian="BigEndian"></Tag>
					<Tag name="NG原因" address="$$64" type="INT16" endian="BigEndian"></Tag>

					<Tag name="叫料编号" address="$$66" type="STR" maxlen="24"></Tag>
				</TagCbnt>
			</TagGrp>
		</TagGrp>

	</TagGrp>


	<!-- 逻辑配置，可以配置0~N个 -->
	<!--<Logicet>Samples.Plugin1.dll</Logicet>-->
</root>
```

## 测点结构

- `<TagGrp/>`：测点群组，可以包含`<TagGrp/>`、`<TagCbnt/>`和`<Tag/>`。
- `<TagCbnt/>`：测点组合，为了减少IO次数而进行统一读写的测点集合。可包含`<Tag/>`
- `<Tag/>`：具体的测点(叶子节点)，没有子元素。每个测点都有一个唯一的地址。

* `channel`：表明节点关联的通道，支持冒泡式向上检索
* `access`：访问方式，分为 RO(只读)、WO(只写)、RW(读写)和 R1W(读一次、后续只写)。
* `type`: 表明测点的数据类型，通常包括 BIT、INT16、INT32、FLOAT、STR 等。


## 测点读写

可以通过`StdUnit.Tags.McpServer`提供的Tools来读写。读写时需要提供测点路径。所谓路径，是指从顶层`<TagGrp/>`导航到子元素的路径，用`/`分隔来元素名(`name`)，类似于`topGrpName/subGrpName/.../optionalCbntName/tagName`
应该先通过`DescribeProject()`工具获取测点项目的静态结构信息(XML)，然后根据静态结构信息来构造测点路径。

