using System.Xml.Linq;

namespace StdUnit.Tags;

/// <summary>
/// 测点项目加载期的校验器（可选功能）。<br/>
/// 每个实现负责一类独立的加载期校验，可注册多个（MS DI 会全部注入并按注册顺序执行），
/// 第三方也可追加自己的校验器：
/// <code>
/// b.AddValidation&lt;TagsProjectSchemaValidator&gt;();            // XSD 校验
/// b.AddValidation&lt;ChannelCrossReferenceValidator&gt;();        // channel 引用校验
/// b.AddValidation&lt;MyValidator&gt;();                            // 自定义校验器
/// </code>
/// 未注册任何实现时，加载期不执行项目校验（默认行为——老的无命名空间前缀 XML 照常工作）。
/// </summary>
public interface ITagsProjectValidator
{
    /// <summary>
    /// 校验项目根元素。不通过时抛出异常（聚合的 <see cref="TagsProjectValidationException"/>）。
    /// </summary>
    /// <param name="root">项目根元素（<c>&lt;Project&gt;</c>）</param>
    void Validate(XElement root);
}
