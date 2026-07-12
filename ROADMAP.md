# MQTT MVC 路线图

本文定义 `MQTTnet.AspNetCore.Routing` 自身的演进路线。该项目是通用 MQTT routing 与 controller 编程模型库,不能绑定任何消费程序的业务 topic、领域对象、权限模型和修复项。

消费程序可以在本库之上定义自己的 topic 契约和消息语义;这些契约属于消费程序,不属于 routing 库路线图。

## 状态标签

- `✅` 已具备:当前代码已经有可用基础。
- `🚧` 进行中:当前路线图、文档或实现正在推进。
- `⬜` 待开始:已确定要做,但还没有实现。
- `⚠️` 待修复:已有实现存在问题或不足,需要优先修正。
- `🧪` 验证中:需要设计验证、兼容性验证或性能验证后再固化。
- `🕒` 后续优化:不是第一批阻塞项,进入稳定性和性能增强阶段。

## 定位

`MQTTnet.AspNetCore.Routing` 的目标是为 MQTTnet 提供类似 ASP.NET Core MVC 的编程体验:

- controller/action 组织方式
- attribute route
- route value 绑定
- payload 绑定
- action result
- filter 管线
- route catalog
- DI 集成
- 测试和诊断工具
- AOT/trimming 友好的显式注册方式

本库只处理 MQTT 消息如何被路由、绑定、调用和返回结果;不规定业务 topic 长什么样,也不规定消息代表什么业务含义。

## 边界

### 本库负责

- MQTT topic route 模板解析和匹配。
- controller 与 action 发现。
- action 参数绑定。
- payload 反序列化扩展点。
- action 调用生命周期。
- action result 到 MQTTnet publish/intercept 语义的映射。
- filter 和 interceptor 管线。
- route metadata、route catalog 和测试辅助工具。
- 性能、AOT、trimming 和稳定性。

### 消费程序负责

- 业务 topic 命名和版本化。
- 认证、授权、身份、权限、资源归属等业务模型。
- payload DTO、schema 和业务校验规则。
- 审计、任务流、工作流、领域事件等业务能力。
- 旧 topic 兼容策略,包括某个 topic 是否废弃、何时删除。
- 业务 controller 的 bug 修复和重构。

### 不做

- 不内置任何特定消费程序或平台的 route convention。
- 不提供任何消费程序领域模型。
- 不重写 MQTTnet broker。
- 不强行套用 HTTP verb、HTTP status code、ProblemDetails 或 OpenAPI 语义。
- 不替消费程序决定 topic 是否兼容、废弃或迁移,也不内置 topic 版本原语。

## 参考模型

### ASP.NET Core MVC

本库借鉴 MVC 的编程模型,而不是 HTTP 协议本身:

- Application Model:启动时描述 controller、action、route、参数、filter 和 metadata。
- Model Binding:从 topic、payload、session、client 信息、user properties 等来源绑定参数。
- Validation Extension:提供可插拔校验点,但不内置业务规则。
- Filters:提供授权、资源、action、异常、result 等管线扩展点。
- Results:提供 MQTT 语义下的接受、拒绝、响应发布和无响应结果。
- Endpoint Discovery:导出 route catalog,帮助测试、文档和工具消费。

借鉴到此为止:凡是 HTTP 特有而 MQTT 没有的概念(verb、status code、header、ProblemDetails),都不进入本库。MQTT 已有的等价物(v5 user properties、reason code、content type)优先使用。

### CoAP.NET Route Adapter

CoAP.NET 的 route adapter 给本库的启发是:协议栈保留协议语义,业务应用在其上定义自己的 route 和分发规则。

MQTT routing 也应保持这个分层:

- MQTTnet 负责 MQTT 协议、连接、订阅、发布和拦截。
- 本库负责 topic 到 action 的编程模型。
- 消费程序负责业务 topic、业务身份和业务处理。

## 上游关系

本库源自 Atlas Lift Tech 的 `MQTTnet.AspNetCore.Routing`(源码版权头保留)。本路线图的 MVC 化是对上游的一次较大分叉,因此需要先确定分叉策略:

- 明确哪些改动可回馈上游,哪些是本仓库长期维护的 patch。
- public API 变更尽量走 extend-only,降低未来与上游合并的成本。
- 若决定长期分叉,需在 README 标注,避免使用者误以为与上游 NuGet 包 API 完全一致。

该决策在 R0 收敛,并约束 R1 之后所有 public API 设计。

## 当前基线

### 已具备

- `✅` `[MqttController]` / `[MqttRoute]` / `MqttBaseController`
- `✅` controller/action discovery,controller route 与 action route 组合
- `✅` route value 到 action 参数的基础绑定
- `✅` `[FromPayload]` JSON payload 绑定
- `✅` `Ok()` / `BadMessage()` 基础结果语义
- `✅` route invocation interceptor
- `✅` MVC 风格 filter 管线
- `✅` 反射式路由(`MqttRouter` + `MqttRouteTable` + `WithAttributeRouting`)
- `✅` slim 委托式路由(`MqttApplicationMessageDispatcher` + `AddMqttApplicationMessageSlimRouting`,基于 `JsonTypeInfo<T>`,dispatch 路径零反射)
- `✅` 显式 controller type 注册方向

### 两套路由并存的现状

当前仓库同时存在两套独立实现,能力不重叠也没有明确分工:

| | 反射式 | slim 委托式 |
| --- | --- | --- |
| 入口 | `WithAttributeRouting` 挂 broker 拦截器 | `IMqttApplicationMessageDispatcher.DispatchAsync` |
| 编程模型 | controller/action + attribute | route 模板 + 委托 handler |
| 绑定 | 运行时反射 | 编译期 `JsonTypeInfo<T>` |
| AOT | 需要硬化 | 天然友好 |
| result/filter | 靠 `MqttBaseController` | 无 |

**这是本路线图当前最大的未决问题**:R1 之后要在哪一套上生长?两套都补 = 重复造轮子;slim 已经具备的 delegate/AOT 优势,不应在反射路径上再实现一遍。收敛策略在 R0 决定(见下文)。

### 缺口

- `⬜` application model 不够显式。
- `⚠️` binding 存在正确性 bug(Guid/enum 转换、错误模型不统一),不只是"缺功能"。
- `⬜` result 类型不够表达化。
- `✅` filter 管线已支持授权、资源、action、异常和 result 扩展点。
- `⬜` route catalog 和测试辅助不足。
- `⚠️` 两套路径都有热路径反射和 payload 复制可优化。
- `??` AOT/trimming 体验需要继续硬化。

## 实现顺序总览

下表是唯一的计划事实源。先修正确性,再固化元数据,再扩展 binding/result/filter,最后做性能与 DX。

| 顺序 | 状态 | 阶段 | 目标 | 前置依赖 |
| --- | --- | --- | --- | --- |
| 1 | `✅` | R0 边界与收敛决策 | 固定库边界、两套路由的收敛策略、上游策略、public/internal API 现状 | 无 |
| 2 | `✅` | R1 绑定正确性 | 统一 model state,修复 Guid/enum/nullable 转换,两路径去除 payload 复制 | R0 |
| 3 | `✅` | R2 Application Model 与 Route Catalog | controller/action/route 元数据显式化,后续 binding/result/filter 都依赖它 | R1 |
| 4 | `✅` | R3 Binding 体系 | MQTT 专用 binding source,替代零散反射绑定 | R2 |
| 5 | `✅` | R4 Result 体系 | MQTT result 与 return type executor | R2、R3 |
| 6 | `✅` | R5 Filter 管线 | 授权、资源、action、异常、result 扩展点 | R2、R3、R4 |
| 7 | `🧪` | R6 性能与 AOT | 缓存热路径、显式注册、trimming 基线 | R1-R5 |
| 8 | `✅` | R7 开发者体验 | 文档、示例、测试辅助、API approval | R1-R6 |

执行原则:

- 先修正确性,再造 MVC 模型。
- 先固化元数据,再扩展 binding、result 和 filter。
- 先保证兼容,再优化性能。
- 先提供通用机制,再让消费程序自行实现业务策略。
- 每个阶段都要补测试,避免把 internal 反射用法扩散到消费程序。

## 分阶段路线图

### ✅ R0:边界与收敛决策

目标:明确本库只做 routing 编程模型,并决定后续在哪套路由上生长。

R0 结论:

- 两套路由采用"并存分工 + 共享元数据"策略,不在 R1 之后强行二选一。
- 反射式 controller 路径继续承载 MVC 风格编程模型:controller/action discovery、attribute route、route value binding、payload binding、`MqttBaseController`、interceptor、后续完整 binding/result/filter 语义。
- slim 委托式路径定位为 AOT/trimming 友好和高吞吐显式注册入口:用户显式声明 route、handler 和 `JsonTypeInfo<T>`,dispatch 热路径不依赖 controller 发现。
- R2 Application Model 与 Route Catalog 要抽象为共享描述层,同时能描述 controller route 和 slim route。R3-R5 的完整 MVC 能力优先落在 controller 路径;slim 路径只接入不破坏 AOT 模型的共享能力,例如 route metadata、catalog、model state、formatter/result 的显式注册版本。
- R6 性能与 AOT 的重点是把反射式 controller 路径逐步迁移到启动期缓存、显式注册或 source-generated delegate,而不是把 slim 已有的 delegate 模型在 controller 路径中重复实现。
- 对现有 public API 采用 extend-only 策略。需要弱化或替代的 API 先标记 `[Obsolete]`,并保留兼容入口。

上游分叉策略:

- 保留 Atlas Lift Tech 原始版权头和 MIT 许可说明。
- 可回馈上游的范围:无业务假设的 bug fix、route matching 修复、性能改进、文档澄清和兼容 MQTTnet 新版本的适配。
- 长期 patch 的范围:MQTT MVC application model、AOT 显式注册、route catalog、filter/result 体系、source-generated 或 provider-neutral 扩展点。
- README 必须明确本项目是 IoTSharp 维护的 fork,避免使用者把后续 MVC/AOT API 误认为上游完全等价能力。

R0 API 基线:

| 类别 | API | 当前定位 |
| --- | --- | --- |
| public stable | `[MqttController]`、`[MqttRoute]`、`[MqttControllerContext]`、`[FromPayload]` | controller 编程模型基础,继续兼容。 |
| public stable | `MqttBaseController`、`MqttControllerContext`、`IMqttControllerContext` | controller action 的上下文和基础结果语义,后续 result 体系以 extend-only 方式补充。 |
| public stable | `AddMqttControllers(...)`、`UseAttributeRouting(...)`、`WithAttributeRouting(...)`、`MqttRoutingOptions` 配置扩展 | 反射式 controller 路径入口,assembly scanning 入口保留并继续标注 trimming 风险。 |
| public stable | `IRouteInvocationInterceptor`、`IPublishEventProvider` | 路由调用和 MQTT publish 事件的通用扩展点。 |
| public preview | `AddMqttApplicationMessageRouting(...)`、`AddMqttApplicationMessageSlimRouting(...)`、`MqttApplicationMessageRouteBuilder`、`IMqttApplicationMessageDispatcher`、`MqttApplicationMessageRouteContext`、`MqttApplicationMessageDispatchResult` | slim 显式路由入口,后续保持 AOT 友好,在 R2-R6 中逐步接入共享 metadata。 |
| internal | `MqttRouter`、`MqttRouteTableFactory`、`MqttRouteTable`、`MqttRoute`、`MqttRouteContext`、template/constraint 类型、`TypeActivatorCache` | controller 路由实现细节,可在不破坏 public API 的前提下重构。 |
| internal | `MqttApplicationMessageRoute`、`MqttApplicationMessageRouteTable` | slim 路由实现细节,不作为外部扩展面。 |

R0 功能清单:

- controller 路径已支持 controller/action 发现、controller route 与 action route 组合、route precedence、catch-all、optional segment、typed route constraints、route value 到 action 参数绑定、`[FromPayload]` JSON payload 绑定、`void` / `Task` action、`Ok()` / `BadMessage()`、route invocation interceptor 和 unmatched route 策略。
- controller 路径已有显式单 controller 注册入口 `AddMqttControllers<TController>()`,并保留 assembly scanning 兼容入口。
- slim 路径已支持显式 `Map(...)` 和 `MapJson<TPayload>(..., JsonTypeInfo<TPayload>, ...)`、route value 提取、`MqttApplicationMessage` 与 `MqttApplicationMessageReceivedEventArgs` dispatch、`IsHandled` / `ProcessingFailed` 回写。
- 当前缺口继续由 R1-R7 承接:统一 model state、Guid/enum/nullable 转换、payload 零复制、共享 application model、route catalog、formatter/result/filter 体系、AOT/trimming 基线和测试辅助。

交付:

- `✅` 更新 README 定位。
- `✅` 更新 ROADMAP。
- `✅` **两套路由的收敛决策**:确定"并存分工 + 共享元数据",并写明后续 R2-R6 的能力落点。
- `✅` **上游分叉策略**:确定可回馈上游 / 长期 patch 的边界。
- `✅` 标记现有 public/internal API。
- `✅` 整理当前功能清单。

验收:

- 文档不出现特定消费程序的业务 route 作为本库计划。
- 业务 topic 只作为 README 示例时出现,且明确是示例。
- 读者能从 ROADMAP 看懂两套路由的关系和演进方向。

### ✅ R1:绑定正确性

目标:先修当前绑定的正确性 bug 和不必要的内存复制,再进入 MVC 化。这些是缺陷,不是新功能。

交付:

- 统一 `MqttModelStateDictionary`:参数缺失、类型转换失败、payload 反序列化失败使用一致的错误表达。
- 统一 binding error code;调试信息进日志,不泄漏进 payload 响应。
- 修复类型转换:当前 `Convert.ChangeType` 对 `Guid`、`enum` 会抛异常,需要改为可扩展的转换器,首版覆盖 string、Guid、int、long、double、decimal、bool、enum、nullable 与 optional。
- 两套路径都去除 payload 复制:反射路径与 slim 路径当前都存在 `Payload.ToArray()`,优先从 `ReadOnlyMemory<byte>` / `ReadOnlySequence<byte>` 读取。
- 补齐绑定与转换失败的测试。

验收:

- route 段为 Guid/enum 的 action 参数能正确绑定,不再抛异常。
- binding failure 结果可预测、可测试。
- JSON 反序列化路径不再无条件 `ToArray()`。

### ✅ R2:Application Model 与 Route Catalog

目标:把 controller/action/route 元数据显式化,启动时生成并缓存。

交付:

- 应用模型:`MqttApplicationModel`、`MqttControllerModel`、`MqttActionModel`、`MqttParameterModel`、`MqttRouteModel`、`MqttFilterModel`,内容涵盖 controller type、action method、route template、route parameters、payload/filter/result metadata、declared content type、custom metadata。
- route catalog exporter:导出 route template、controller、action、parameter list、binding source、payload type、result type、custom metadata、filter metadata。
- route ambiguity detection 与 startup diagnostic report。
- route catalog / route table 快照测试辅助。

目标收益:

- 减少运行时反射,支持启动时冲突检测与测试快照。
- 消费程序可在启动或测试阶段读取 catalog、检查 route 约束。

catalog 必须保持业务中立,不内置任何消费程序的 route 分类。route 的 obsolete/版本/迁移策略属于消费程序,不在此内置。

验收:

- 消费程序可以在测试中读取 route catalog。
- route 冲突能在启动或测试阶段暴露。

### ✅ R3:Binding 体系

目标:在 R1 修好的绑定基础上,补齐 MQTT 专用 binding source 和上下文类型。

交付:

- binding source:`[FromMqttRoute]`、`[FromMqttPayload]`、`[FromMqttSession]`、`[FromMqttClient]`、`[FromMqttUserProperty]`、`[FromMqttContext]`。
- 上下文类型(不携带业务身份假设):
  - `MqttRequestContext`:clientId、topic、payload、qos、retain、response topic、correlation data、user properties、session items、cancellation token。
  - `MqttRouteContext`:matched route、route values、action descriptor。
  - `MqttActionContext`:request context、route context、model state、request services、logger scope。
- 直接绑定支持:JSON payload DTO、raw payload(`byte[]` / `ReadOnlyMemory<byte>` / `ReadOnlySequence<byte>` 可行性评估)、`MqttApplicationMessage`、`MqttRequestContext` / `MqttActionContext`。
- Payload formatter 抽象:`IMqttPayloadInputFormatter` / `IMqttPayloadOutputFormatter`,内置 JSON 与二进制两种;文本及 XML/MessagePack/Protobuf/CSV 等由消费程序按需扩展,本库不内置。
- formatter 选择依据:attribute 显式声明、MQTT v5 content type、route metadata、fallback 默认配置。

context 应能从 `InterceptingPublishEventArgs` / `MqttApplicationMessageRouteContext` 适配,同时不阻断现有 `MqttBaseController` 使用方式。

当前进展:

- `✅` 已新增 `[FromMqttRoute]`、`[FromMqttPayload]`、`[FromMqttSession]`、`[FromMqttClient]`、`[FromMqttUserProperty]`、`[FromMqttContext]`,并保留旧 `[FromPayload]` 兼容入口。
- `✅` 已新增 `MqttRequestContext`、`MqttRouteContext`、`MqttActionContext`,controller 路径和 slim 路径都能适配到同一套上下文。
- `✅` 已新增 `IMqttPayloadInputFormatter` / `IMqttPayloadOutputFormatter` 及内置 JSON、binary formatter。
- `✅` controller action 参数已接入统一 binder,支持 route、JSON payload、raw payload、session item、clientId/userName、user property 和上下文类型绑定。
- `✅` formatter 选择已支持参数声明、MQTT v5 content type、route metadata 和默认配置兜底。
- `✅` route catalog 已导出 R3 binding metadata,包括绑定名称、payload content type 和 formatter 名称；controller 与 slim route 均有测试覆盖。
- `➡️` result 体系复用 output formatter 留给 R4;测试辅助 API 继续放在 R7。

验收:

- action 参数不必手动读取原始 message 即可绑定 route、payload、session、client 信息与 user properties。
- 消费程序可注册自定义 formatter,而本库默认只依赖 JSON + 二进制。

### ✅ R4:Result 体系

目标:让 action 返回值表达 MQTT 处理结果,而不是只依赖 `Ok()` / `BadMessage()`。

交付:

- result 类型:`MqttResult`、`MqttAcknowledgeResult`、`MqttSuppressResult`、`MqttRejectResult`、`MqttPublishResult`、`MqttPayloadResult<T>`。
- return type executor,支持 `void`、`Task`、`ValueTask`、`MqttResult`、`Task<MqttResult>`、`ValueTask<MqttResult>`、`T`、`Task<T>`、`ValueTask<T>`;其中 `T` 通过 output formatter 变成 response payload result。
- output formatter 初版(复用 R3 的 formatter 抽象)。
- `MqttBaseController` 保留旧 `Ok()` / `Accepted()` / `BadMessage()` 兼容 helper,新增 `Acknowledge()` / `Suppress()` / `Reject()` / `Publish()` / `Payload<T>()`。

设计要求:

- 不套 HTTP status code,也不引入 ProblemDetails。拒绝/异常用 MQTT v5 reason code / reason string 表达。
- result 先表达**入站 PUBLISH 处置**:`Acknowledge` 继续 broker 原始消息投递,`Suppress` 表示 action 已消费但对发送端成功确认,`Reject` 表示拒绝并写入 PUBACK reason code。
- 正常接收 PUBLISH 不自动产生业务响应;只有 `MqttPublishResult` / `MqttPayloadResult<T>` 或返回 `T` 且请求带 MQTT v5 response topic 时才额外向 topic 发布响应。
- 能映射到 `InterceptingPublishEventArgs.ProcessPublish`,选择是否继续 broker 原始消息投递。
- 能向指定 topic 或 MQTT v5 response topic 发布响应,并携带 correlation data。
- 能被 result filter 观察和修改。
- **AOT 约束**:return type executor 处理 `T` / `Task<T>` / `ValueTask<T>` 时,不得用 `MakeGenericMethod` 在运行时拆包(会触发 IL3050)。AOT 安全的做法只有两条——注册期捕获强类型委托(slim 路径的 `Func<..., TPayload, ValueTask>` 已是此模式),或由 source generator 生成每个 `T` 的 executor。这条约束决定了"支持 `Task<T>`"能否不把库拖出 AOT。output formatter 同理走 `JsonTypeInfo<T>`,不做运行时泛型反射。

当前进展:

- `✅` controller 路径已接入 result executor,旧 `void` / `Task` action 行为保持不变。
- `✅` `MqttResult` 可控制 `ProcessPublish`、PUBACK reason code / reason string、close connection 和 user properties。
- `✅` `MqttPayloadResult<T>` 复用 R3 output formatter,默认使用请求 response topic,不会为普通 PUBLISH 自动造响应 topic。
- `✅` `MqttPublishResult` 通过 `MqttServer.InjectApplicationMessage` 注入响应消息,并使用内部 session 标记避免响应消息被本 router 二次路由拦截。
- `✅` `Task<T>` / `ValueTask<T>` executor 不使用 `MakeGenericMethod`;controller 反射路径的拆包反射被限制在小 helper 中,R6 再替换为注册期委托或 source generator。

验收:

- action 可以返回 `Task<MqttResult>` 或 `T`。
- result 能控制是否继续原始 publish,并能发布响应 topic。
- return type executor 在 Native AOT 下无 IL3050 警告。

### ✅ R5:Filter 管线

目标:提供 MVC 风格横切扩展点。

交付:

- filter 接口:`IMqttAuthorizationFilter`、`IMqttResourceFilter`、`IMqttActionFilter`、`IMqttExceptionFilter`、`IMqttResultFilter`,含 ordering 与 metadata。
- 内置通用 filter:payload size limit、model state invalid、exception 转 result、metrics、logging scope。
- **异常默认语义**:当前实现中 action 抛异常等于 `ProcessPublish=false`(拒绝投递)。引入 exception filter 时必须先声明默认行为(默认拒绝),保持与现有语义兼容,变更走 opt-in。

业务授权、资源隔离、业务审计等 filter 由消费程序实现,本库不内置业务规则。

当前进展:

- `✅` 已新增 `IMqttAuthorizationFilter`、`IMqttResourceFilter`、`IMqttActionFilter`、`IMqttExceptionFilter`、`IMqttResultFilter` 和 ordered metadata。
- `✅` controller 路径已接入 filter provider 与执行管线,执行顺序为 authorization -> resource -> action -> exception -> result。
- `✅` 已内置 payload size limit、model state invalid、exception 转 reject result、metrics 和 logging scope filter。
- `✅` 默认异常 filter 保持历史拒绝语义；消费程序可通过自定义 exception filter opt-in 恢复为 acknowledge/suppress 等 result。
- `✅` route catalog 已导出 controller/action attribute filter metadata。
- `✅` global filter 支持类型注册和实例注册；类型注册路径已补 trimming 注解,避免 `ActivatorUtilities` 的 public constructor 需求断链。

验收:

- 消费程序可以通过 filter 实现自己的授权、审计、资源隔离和业务校验。
- 引入 filter 后,未配置 filter 的现有 controller 行为不变。

### 🚧 R6:性能与 AOT

目标:在模型稳定后减少热路径开销,强化 AOT/trimming。注意:slim 路径已具备 delegate/`JsonTypeInfo` 优势,本阶段重点是把反射路径拉齐或收敛,而非重复实现 slim 已有能力。

交付:

- 缓存:controller activator、controller context setter、parameter binder、action invoker delegate、filter pipeline。
- **禁用运行时泛型反射**:activator、binder、return-type executor 一律走注册期强类型委托或 source generator,不得用 `MakeGenericMethod` / `Activator.CreateInstance(Type)` 之类在热路径拆包(承接 R1 转换器与 R4 executor 的 AOT 约束)。
- route matching:启动时构建 segment index 或 trie,预计算 route precedence,补 catch-all 与 optional segment 测试,大小写规则明确且可配置。
- formatter:System.Text.Json source generation、formatter selection cache、per-route JSON options/context、binary/text 快路径。
- 显式注册:`AddMqttControllers<TController>()`、`AddMqttControllers(params Type[])`;已交付独立 `MQTTnet.AspNetCore.Routing.SourceGeneration` v1,对 opt-in controller 生成 route、DI 构造与直接 action 委托,后续扩展异步返回、更多绑定源和完整 filter 管线。
- public API 标注 trimming 相关 attribute,trimming analyzer clean baseline。

验收:

- 高吞吐场景下没有明显反射热点。
- Native AOT 消费程序有清晰推荐用法。

### ✅ R7:开发者体验

目标:让库作为通用 NuGet 包更容易使用和维护。

交付:

- `✅` 文档:controller routing、binding、payload formatter、result、filter、route catalog、AOT、migration、performance guide。
- `✅` 测试辅助 API:构造 `MqttApplicationMessage` / fake `MqttRequestContext`、执行 route matching 与 action invocation、断言 result、读取 route catalog、快照 route table。
- `✅` 示例入口更新:README 增加 docs 与 testing helper 入口,示例项目继续作为 server/client 基线。
- `✅` test helper namespace:`MQTTnet.AspNetCore.Routing.Testing`。
- `✅` API approval 测试:`PublicApi_MatchesApprovedSnapshot` 固化 public API 快照。

验收:

- `✅` 新使用者能按文档创建 controller、绑定 payload、返回 result、添加 filter。
- `✅` 消费程序可以为自己的业务 topic 写测试,而不必反射内部类型。
- `✅` public API 变更能被测试发现。

## 可观测性(贯穿 R5-R7)

通用指标(业务字段由消费程序自行加入):

- route matched / unmatched count
- binding failure count
- action failure count
- result rejected count
- action latency
- payload size histogram

通用日志 scope:`mqtt.client_id`、`mqtt.topic`、`mqtt.route`、`mqtt.action`、`mqtt.correlation_data`、`trace_id`。

## API 兼容原则

- 公共 API 采用 extend-only 策略。
- 已发布 public member 不直接删除或改签名。
- 新能力通过新类型、新 overload、新 attribute 增加。
- 废弃 API 先标 `[Obsolete]`,并说明替代 API。
- 行为变化默认 opt-in(包括异常语义、路由大小写规则等)。
- wire/payload 格式不由本库强行定义。

## 完成定义

MQTT MVC 第一版完成时,本库应满足:

- 两套路由的关系已收敛并在文档中说明。
- 绑定正确(Guid/enum/nullable 无异常),错误模型统一。
- controller/action 模型稳定,元数据显式。
- binding source 清晰,formatter 可扩展。
- result 模型可表达 MQTT 处理结果,不套 HTTP 语义。
- filter 管线可扩展,默认语义兼容现状。
- route catalog 可导出且业务中立。
- route 和 action 可测试。
- AOT/trimming 有推荐路径。
- 文档不绑定任何消费程序业务协议。
