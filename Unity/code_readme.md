# ET代码阅读记录

## ET术语&机制

### 代码生成
ET中有很多自动生成的代码，如[[EntitySystem]](../Share/Share.SourceGenerator/Generator/ETSystemGenerator/ETSystemGenerator.cs)主要依赖于 C# 的 Source Generator（源代码生成器）技术‌，结合特定的特性（Attribute）标记，在编译阶段自动推断并生成对应的 System 类文件，从而避免手动编写重复模板代码。  
搜索 ISourceGenerator 可以找到相关代码在 ET/Share/Share.SourceGenerator/Generator/ 文件夹下  
参考:  [Roslyn 技术解析：如何利用它做代码生成？](https://blog.csdn.net/2501_94611820/article/details/155851773) 、   [聊一聊 C#中有趣的 SourceGenerator生成器](https://zhuanlan.zhihu.com/p/778871873)

**改进点**：用c#泛型，即template\<T>可以实现自动代码生成的相关功能，代码阅读起来更容易一些。


### SceneType变更
在[EntryEvent3_InitClient](Assets/Scripts/HotfixView/Client/Demo/EntryEvent3_InitClient.cs) 中会修改Client中SceneType.Main 为 globalComponent.GlobalConfig.AppType，如修改为 Demo、LockStep，从而使得一些消息Hanlder可以正常工作。

### ECS
Entity & Componet & System（实体、组件、系统），类似于MVC  
实体是数据  
组件是视图，是表现层，代表了一组功能  
系统是具体功能的实现。 
项目[Book/3.3](../Book/3.3一切皆实体.md)介绍的非常详细了。  

| 一些组件                                 | 作用         |
|:-------------------------------------|:-----------|
| [ProcessInnerSender](#pisender-call) | 进程内部消息转送组件 |
| [NetComponent](#netcomponent)        | 网络数据的收发组件  |
| [Session](#session)                  | 会话组件       |
| [MoveComponent](#MoveComponent)      | 角色移动组件     |


### ETTask
异步任务，类似C#中的Task。用C#原生的Task应该也可以实现ETTask相关功能，更简洁。

### Fiber：纤程
线程中可以异步执行的子任务，相当于一个Task，由ISchedulers去调度执行。
- 服务端与客户端Fibers区别：
  - 客户端的MainThreadScheduler Update/LaterUpdate由Unity驱动，服务器启动了一个线程来驱动
  - [EntryEvent2_InitServer代码](Assets/Scripts/Hotfix/Server/Demo/EntryEvent2_InitServer.cs)中启动的Fibers都是ThreadPoolScheduler类型，并且设置了对应的配置文件中的SceneType类型（用于事件过滤）。

### Schedulers
| 类型                  |   作用    |                            细节 |
|:--------------------|:-------:|------------------------------:|
| MainThreadScheduler | 主线程调度器  |      主线程中调度Update/LaterUpdate |
| ThreadScheduler     |  线程调度器  |           启动一个线程，线程执行Loop()函数 |
| ThreadPoolScheduler | 线程池调度器  | 启动多个线程并放到线程池中，每个线程都执行Loop()函数 |

### 客户端Update链路
[Init: MonoBehaviour](Assets/Scripts/Loader/MonoBehaviour/Init.cs)
- Update():
  - TimeInfo.Instance.[Update](Assets/Scripts/Core/World/Module/TimeInfo/TimeInfo.cs)();
  - FiberManager.Instance.[Update](Assets/Scripts/Core/World/Module/Fiber/FiberManager.cs)():
    - this.mainThreadScheduler.[Update](Assets/Scripts/Core/World/Module/Fiber/MainThreadScheduler.cs)():
      - this.threadSynchronizationContext.Update();
      - foreach fiber.[Update](Assets/Scripts/Core/Fiber/Fiber.cs)():
        - this.EntitySystem.[Update](Assets/Scripts/Core/Fiber/EntitySystem.cs)():
          - ```csharp
            foreach component in this.queues[InstanceQueueIndex.Update];   
                foreach iUpdateSystem in GetSystems(component, IUpdateSystem);  
                    iUpdateSystem.Run();
            ``` 
- LateUpdate():
  - FiberManager.Instance.LateUpdate():
    - this.mainThreadScheduler.LateUpdate():
      - foreach fiber.LateUpdate():
        - this.EntitySystem.LateUpdate():
          - ```csharp
            foreach component in this.queues[InstanceQueueIndex.LateUpdate];   
                foreach iLateUpdateSystem in GetSystems(component, ILateUpdateSystem);  
                    iLateUpdateSystem.Run();
            ``` 
        - FrameFinishUpdate();
        - this.ThreadSynchronizationContext.Update();

  
### 网络相关
#### TCP & UDP &socket
|                                                                          |
|:----------------------------------------------------------------------------|
| [【socket笔记】TCP、UDP通信总结](https://cloud.tencent.com/developer/article/1545369)|
| [TCP/UDP/Socket 通俗讲解](https://zhuanlan.zhihu.com/p/686583180)               |

TCP 和 UDP 位于 TCP/IP 协议栈的‌传输层‌，而 Socket 并不位于协议栈的某一层，它是‌应用层与传输层之间的编程接口抽象层‌，用于实现网络通信。  

一、TCP 与 UDP：传输层的核心协议
在 TCP/IP 模型中，传输层负责端到端的数据传输控制，主要协议就是 TCP（传输控制协议）和 UDP（用户数据报协议）：
- TCP‌：面向连接、可靠传输，通过三次握手建立连接、确认机制、重传、流量控制等确保数据完整有序，适用于 HTTP、FTP、SMTP 等对可靠性要求高的场景。 
- UDP‌：无连接、不可靠但高效，不保证顺序和重传，适用于 DNS、视频流、实时语音等对延迟敏感的场景。
两者都工作在‌传输层‌，利用端口号标识应用程序进程，实现多任务并发通信。

二、Socket：不是协议，而是通信接口
Socket（套接字）并不是一个协议，也不是协议栈中的一层，而是操作系统提供的一组 API，作为‌应用层与传输层之间的桥梁‌：
- 它封装了 TCP/IP 协议族的复杂细节，让开发者可以通过简单的函数调用（如 socket()、connect()、send()）完成网络通信。
- 每个 Socket 由 IP 地址 + 端口号 + 协议类型‌ 唯一标识，形成一个通信端点。
- 无论是基于 TCP 还是 UDP 的应用，都需要通过 Socket 接口与内核中的协议栈交互。  

举个比喻：如果把 TCP/IP 协议栈比作邮政系统，那么 IP 是地址系统，TCP/UDP 是信件的投递方式（挂号信 vs 普通信），而 Socket 就是你去邮局寄信时填写单据、递交包裹的那个“窗口接口”。

#### KCP 
||
|-|
|[KCP协议：从TCP到UDP家族QUIC/KCP/ENET](https://cloud.tencent.com/developer/article/1964393)|
|[KCP 协议：为流速和低延时设计的协议丨音视频基础](https://cloud.tencent.com/developer/article/2021491)| 

随着网络技术飞速发展，网速已不再是传输的瓶颈，CDN服务商Akamai报告从2008年到2015年7年时间，各个国家网络平均速率由1.5Mbps提升为5.1Mbps，网速提升近4倍。网络环境变好，网络传输的延迟、稳定性也随之改善，UDP的丢包率低于5%，如果再使用应用层重传，能够完全确保传输的可靠性。  
KCP协议就是在保留UDP快的基础上，提供可靠的传输，应用层使用更加简单——TCP可靠简单，但是复杂无私，所以速度慢。KCP尽可能保留UDP快的特点下，保证可靠。
- TCP是为流量设计的（每秒内可以传输多少KB的数据），讲究的是充分利用带宽。
- KCP是为流速设计的（单个数据包从一端发送到一端需要多少时间），以10%-20%带宽浪费的代价换取了比 TCP快30%-40%的传输速度。  

TCP信道是一条流速很慢，但每秒流量很大的大运河，而KCP是水流湍急的小激流。  
不同传输层协议在可靠性、流量控制等方面都有差别，而这些技术细节会对延迟造成影响。  
tcp追求的是完全可靠性和顺序性，丢包后会持续重传直至该包被确认，否则后续包也不会被上层接收，且重传采用指数避让策略，决定重传时间间隔的RTO(retransmission timeout)不可控制，linux内核实现中最低值为200ms，这样的机制会导致丢包率短暂升高的情况下应用层消息响应延迟急剧提高，并不适合实时性高、网络环境复杂的游戏。  
基于udp定制传输层协议，引入顺序性和适当程度或者可调节程度的可靠性，修改流控算法。适当放弃重传，如：设置最大重传次数，即使重传失败，也不需要重新建立连接。比较知名的tcp加速开源方案有：quic、enet、kcp、udt。

KCP是一个快速可靠协议，能以比 TCP浪费10%-20%的带宽的代价，换取平均延迟降低 30%-40%，且最大延迟降低三倍的传输效果。  
纯算法实现，并不负责底层协议（如UDP）的收发，需要使用者自己定义下层数据包的发送方式，以 callback的方式提供给 KCP。 连时钟都需要外部传递进来，内部不会有任何一次系统调用。

**KCP的实现细节：**  
- ‌基于UDP‌：KCP底层使用了UDP协议来传输数据包，因为UDP提供了低延迟的特性，这对于游戏等实时应用非常重要。
- ‌封装UDP‌：KCP在UDP的基础上增加了一层封装，这层封装包括了数据包的序列号、时间戳等控制信息，用于实现其特有的流量控制和拥塞控制机制。
- ‌不纯粹的TCP特性‌：虽然KCP使用了UDP的传输机制，但它通过内部的机制（如滑动窗口、拥塞控制算法等）模拟了TCP的一些特性（如可靠性、流量控制），从而在保证低延迟的同时提高了数据传输的稳定性。
#### KService & TService
在 ET 框架中，KService、Session、TChannel 和 UDP Socket 之间的关系是构建其网络通信机制的重要组成部分。以下是它们之间的关系和作用：

1. KService 与 UDP Socket
  - KService 是 ET 中用于处理 UDP 协议‌ 的服务类，它封装了 UDP 的监听和连接逻辑。
  - 它内部使用 UdpClient 来创建 UDP 套接字（Socket），用于接收和发送数据包。
  - 在服务端，KService 会监听特定的 IP 地址和端口，等待来自客户端的 UDP 数据包。
  - KService 负责管理多个 UDP 连接，每个连接对应一个 Channel‌（如 KChannel）。    
KService 与 UDP Socket 的关系是：KService 是对 UDP Socket 的封装，用于处理 UDP 的连接和通信。

2. TChannel 与 TCP Socket
  - TChannel 是 ET 中用于处理 TCP 连接的底层通道类。
  - 它内部使用 Socket 类来创建 TCP 套接字，用于建立与客户端的连接。
  - 每个 TChannel 对应一个 TCP 连接，负责该连接的数据收发。
  - TChannel 会通过异步方式读取和发送数据，并将数据交给上层处理。  
TChannel 与 TCP Socket 的关系是：TChannel 是对 TCP Socket 的封装，用于处理 TCP 连接的读写操作。

3. Session 与 Channel
  - Session 是 ET 中对一个连接的高层封装，它基于 Channel（如 TChannel 或 KChannel）实现。
  - Session 提供了更高层的接口，用于发送和接收消息，并负责消息的序列化和反序列化。
  - Session 与 Channel 之间是一对一的关系，一个 Session 对应一个 Channel。
  - Session 是上层业务逻辑与底层网络通信之间的桥梁。  
   Session 是对 Channel 的封装，提供业务层的接口和功能。

4. KService 与 Session 的关系‌
  - KService 通常会管理多个 KChannel（UDP 连接），而每个 KChannel 会对应一个 Session。
  - 当一个 UDP 连接建立后，KService 会创建一个 KChannel，并将其与一个 Session 关联起来。
  - Session 负责处理该连接上的消息逻辑。  
KService 通过 KChannel 管理 UDP 连接，而每个 KChannel 对应一个 Session。

5. TChannel 与 Session 的关系‌
  - 类似地，TChannel 与 Session 也是一对一的关系。
  - 当一个 TCP 连接建立后，TService 会创建一个 TChannel，并将其与一个 Session 关联。
  - Session 负责处理该连接上的业务逻辑。  
TChannel 与 Session 之间是一对一的绑定关系，TChannel 是底层通信通道，Session 是高层封装。



| __总结关系图__   |    |   __总结关系图__  |
|:---------------------:|----|:----------------------:|
|    KService (UDP)     |    |     TService (TCP)     |
|           ↓           |    |           ↓            |
| KChannel (UDP Socket) |    | TChannel (TCP Socket)  |
|           ↓           |    |           ↓            |
|  Session (业务逻辑层)      |    |    Session (业务逻辑层)     |
- KService 和 TService 是服务端的监听组件，分别处理 UDP 和 TCP 连接。
- KChannel 和 TChannel 是底层的连接通道，分别对应 UDP 和 TCP 的 Socket。
- Session 是上层的会话封装，负责业务逻辑处理，与 Channel 一一对应。

这些组件共同构成了 ET 框架的网络通信机制，支持 TCP、UDP 和 WebSocket 等多种协议。


#### [ET Session](Assets/Scripts/Model/Share/Module/Message/Session.cs) <a id="session"></a>
在 ET 框架中，Session 通过底层的 KChannel/TChannel 与客户端和服务端进行通信。客户端和服务端都维护一个与对方的 Socket 连接，这个连接通过 KChannel/TChannel 管理，Session 是对这个连接的封装。每个连接都有一个唯一的 Socket 套接字链接 ID，用于区分不同的连接。

##### Session 的通信流程‌
1. 连接建立‌：
- 客户端通过 NetClientComponent.Create() 方法创建一个 Session，该方法会建立与服务器的 TCP 连接。
- 服务器端通过 Accept() 方法监听客户端连接请求，当连接建立后，会创建一个 TChannel 对象，并将其包装成一个 Session 对象。

2. 底层通信‌：
- Session 通过底层的 KChannel/TChannel 进行实际的数据传输。KChannel/TChannel 是一个封装了 Socket 连接的类，负责实际的网络读写操作。
- 客户端和服务端都会维护一个与对方的 Socket 连接，这个连接通过 KChannel/TChannel 管理。Session 本身是上层逻辑对这个连接的封装。

3. 消息传递‌：
- 客户端通过 Session.Send() 方法发送消息给服务器，消息会通过底层的 KChannel/TChannel 发送到服务器。
- 服务器端通过 Session 接收客户端发送的消息，并处理这些消息。

##### Socket 套接字链接 ID
- 客户端和服务端都保持 Socket 套接字链接‌：
  - 客户端和服务端都维护一个与对方的 Socket 连接，这个连接在底层通过 KChannel/TChannel 管理。
  - 每个连接都有一个唯一的标识符，即 Socket 套接字链接 ID。这个 ID 是操作系统分配给每个 UDP/TCP 连接的，用于区分不同的连接。
- Session 与 Socket 的关系‌：
  - Session 是对底层 Socket 连接的封装，它包含了一个 KChannel/TChannel 对象，该对象持有实际的 Socket 连接。
  - 因此，Session 与 Socket 套接字链接 ID 是一一对应的，Session 通过 TChannel 操作底层的 Socket。

#####  具体实现 TODO
- Call
- Send

### 开源物理引擎
为了保证帧同步服务端与客户端计算结果一直，一般使用int物理引擎，开源的如 BEPUphysicsInt  
[BEPU物理引擎碰撞系统的架构与设计](https://zhuanlan.zhihu.com/p/549276185)


## ET启动流程
![启动流程图片](readme_imgs/00_Start.jpg)  
服务端和客户端执行流程大致相同，客户端入口[init.cs](Assets/Scripts/Loader/MonoBehaviour/Init.cs)，服务端入口[Program.cs](../DotNet/App/Program.cs)。以客户端为例，主要过程如下：  
- 创建world单例（调用world.Instance时自动创建)，作为所有单例的管理仓库。
- 设置配置文件
- 添加日志单例[UnityLogger](Assets/Scripts/Loader/UnityLogger.cs)，并位ETTask.ExceptionHandler事件注册Log.Error回调函数。
- 添加[TimeInfo单例](Assets/Scripts/Core/World/Module/TimeInfo/TimeInfo.cs): 每帧更新当前和服务器时间。
- 添加[FiberManager单例](Assets/Scripts/Core/World/Module/Fiber/FiberManager.cs)：每帧更新主线程调度器，进而执行ET实现的Fiber任务。
- 添加[ResourcesComponent单例](Assets/Scripts/Loader/Resource/ResourcesComponent.cs)：YooAssets管理资源包，下载与加载？
- 添加[CodeLoader单例](Assets/Scripts/Loader/CodeLoader.cs)，并下载代码资源
  - ResourcesComponent下载需要的dll，并加载
  - 加载hotfix
  - 调用ET.Entry.start
- [ET.Entry.start](Assets/Scripts/Model/Share/Entry.cs)流程
  - 注册mongo
  - 注册Entity序列化器
  - 添加单例IdGenerater
  - 添加单例[OpcodeType](Assets/Scripts/Core/Network/OpcodeType.cs)：对所有[[Message]](Assets/Scripts/Core/Network/MessageAttribute.cs)建立[type,opcode]字典，以及response类型映射。
  - 添加单例ObjectPool
  - 添加单例MessageQueue
  - 添加单例NetServices
  - 添加单例NavmeshComponent
  - 添加单例LogMsg
  - 创建需要[reload的code singleton](Assets/Scripts/Core/World/Module/Code/CodeTypes.cs)：初始化所有[[code]](Assets/Scripts/Core/World/Module/Code/CodeAttribute.cs)，即建立对应事件与回调字典。
    - [EventSystem](Assets/Scripts/Core/World/Module/EventSystem/EventSystem.cs)：初始化并注册所有的[Event]、[Invoke]的handler实例。
    - [MessageDispatcher](Assets/Scripts/Core/World/Module/Actor/MessageDispatcher.cs)：初始化并注册所有的[MessageHandler]、[MessageLocationHandler]
    - [EntitySystemSingleton](Assets/Scripts/Core/Entity/EntitySystemSingleton.cs)：初始化并注册所有的[EntitySystem]
    - [HttpDispatcher](Assets/Scripts/Model/Server/Module/Http/HttpDispatcher.cs)：初始化并注册所有的[HttpHandler]
    - [LSEntitySystemSingleton](Assets/Scripts/Model/Share/LockStep/LSEntitySystemSingleton.cs)：初始化并注册所有的[LSEntitySystem]
    - [AIDispatcherComponent](Assets/Scripts/Model/Share/Module/AI/AIDispatcherComponent.cs)：初始化并注册所有的[AIHandler]
    - [ConsoleDispatcher](Assets/Scripts/Model/Share/Module/Console/ConsoleDispatcher.cs)：初始化并注册所有的[ConsoleHandler]
    - [MessageSessionDispatcher](Assets/Scripts/Model/Share/Module/Message/MessageSessionDispatcher.cs)：初始化并注册所有的[MessageSessionHandler]
    - [NumericWatcherComponent](Assets/Scripts/Model/Share/Module/Numeric/NumericWatcherComponent.cs)：初始化并注册所有的[NumericWatcher]
    - [UIEventComponent](Assets/Scripts/ModelView/Client/Module/UI/UIEventComponent.cs)：初始化并注册所有的[UIEvent]
  - 添加ConfigLoader
  - 创建Main Fiber，会发布FiberInit事件，进而调用事件处理器[FiberInit_Main](Assets/Scripts/Hotfix/Share/FiberInit_Main.cs)
    - [EntryEvent1_InitShare](Assets/Scripts/Hotfix/Share/Demo/EntryEvent1_InitShare.cs)：向MainFiber添加组件 TimerComponent 、CoroutineLockComponent、ObjectWait、MailBoxComponent、ProcessInnerSender组件
    - [EntryEvent2_InitServer](Assets/Scripts/Hotfix/Server/Demo/EntryEvent2_InitServer.cs)：创建服务配置文件中的一系列纤程，包括Gate，Router，Match等。
    - [EntryEvent3_InitClient](Assets/Scripts/HotfixView/Client/Demo/EntryEvent3_InitClient.cs)：向MainFiber添加一系列组件 GlobalComponent、UIGlobalComponent、UIComponent、ResourcesLoaderComponent、PlayerComponent、CurrentScenesComponent，并发布[AppStartInitFinish]事件。

## ET登录流程
参考 [b站 【ET框架 -- 登录流程】 by 和v诺](https://www.bilibili.com/video/BV1Rr26YFED2?vd_source=806cbed30e2817314f6d8f3b290f03e4)  
![登录导图](readme_imgs/01_Login.jpeg)  
 
### 客户端登录请求流程
- 创建登录UI：[AppStartInitFinish_CreateLoginUI](Assets/Scripts/HotfixView/Client/Demo/UI/UILogin/AppStartInitFinish_CreateLoginUI.cs) 创建并绑定登录回调
  - [UIHelper](Assets/Scripts/HotfixView/Client/Demo/UI/UIHelper.cs)调用[UIComponent](Assets/Scripts/HotfixView/Client/Module/UI/UIComponentSystem.cs)中的create
  - 根据事件类型，最终会调用到[UILoginEvent 事件处理器](Assets/Scripts/HotfixView/Client/Demo/UI/UILogin/UILoginEvent.cs)
    - ResourcesLoaderComponent 加载UI资源，初始化UI GameObject
    - 添加 [UILoginComponent](Assets/Scripts/HotfixView/Client/Demo/UI/UILogin/UILoginComponentSystem.cs): Awake时会绑定登录按钮回调函数为自己的OnLogin
- 登录按钮处理事件：调用[LoginHelper.Login](Assets/Scripts/Hotfix/Client/Demo/NetClient/LoginHelper.cs)进行实际的登录流程
  - 移除/添加 [ClientSenderComponent]，调用[ClientSenderComponent.LoginAsync](Assets/Scripts/Hotfix/Client/Demo/Main/ClientSenderComponentSystem.cs)向服务器请求PlayerId
    - 创建[Fiber_NetClient]纤程：用于实际网络数据的收发
    - 创建[Main2NetClient_Login]登录请求消息
    - 调用[ProcessInnerSender.Call](#pisender-call)发送登录请求消息给网络纤程，并异步等待返回结果
  - 设置PlayerComponent.playerId为服务器返回的PlayerId
  - 发布[**LoginFinsh**]事件

#### 登录网络处理细节
- [ProcessInnerSender.Call<a id="pisender-call"></a>](Assets/Scripts/Core/Fiber/Module/Actor/ProcessInnerSenderSystem.cs)：发送登录请求消息[Main2NetClient_Login]给网络纤程，并异步等待返回结果
  - 把请求消息放到 [MessageQueue](Assets/Scripts/Core/World/Module/Actor/MessageQueue.cs)中
  - 添加消息发送结构体 MessageSenderStruct
  - 设置超时，并开始异步等待服务器返回结果。
  - - 实际的收发消息在 [ProcessInnerSender.Update](Assets/Scripts/Core/Fiber/Module/Actor/ProcessInnerSenderSystem.cs) 中异步完成 
    - 从 MessageQueue 取出消息，按类型处理：
      - 如果是Response，则SetResult，使ETTask结束await
      - 如果是Request，则调用[MailBoxComponent.Add](Assets/Scripts/Core/Fiber/MailBoxComponent.cs)，内部实现是发布一个[MailBoxInvoker]事件。
- [Fiber_NetClient](Assets/Scripts/Hotfix/Client/Demo/NetClient/FiberInit_NetClient.cs): 网络数据收发
    - [MailBoxType_UnOrderedMessageHandler](Assets/Scripts/Hotfix/Share/Module/Actor/MailBoxType_UnOrderedMessageHandler.cs)会处理[MailBoxInvoker]事件，内部调用 MessageDispatcher.handle
      - [MessageDispatcher.handle](Assets/Scripts/Core/World/Module/Actor/MessageDispatcher.cs)找到消息对应的handler ： Main2NetClient_LoginHandler
        - [Main2NetClient_LoginHandler](Assets/Scripts/Hotfix/Client/Demo/NetClient/Main2NetClient_LoginHandler.cs) 执行实际的登录请求，具体过程：
          - 获取Gate地址
            - 创建[NetComponent]:负责数据的收发
            - 获取Realme地址
            - 使用Realm地址[创建session](#crsession) ，
            - 并使用[session.Call](#session)发送[C2R_Login](#c2r_loginhandler) 请求
            - 等待RouterSession返回的[R2C_Login]响应
          - 建立GateSession
            - 使用R2C_Login中的地址[创建session](#crsession) 
            - 用SessionComponent组件保存当前gateSession
          - 通过GateSession登录Gate，获取PlayerId。
            - 创建[C2G_LoginGate](#c2g_logingatehandler)请求
            - 发送给Gate，并等待返回结果
            - 获取响应G2C_LoginGate中的PlayerId
- [NetComponent<a id="netcomponent"></a>](Assets/Scripts/Hotfix/Share/Module/Message/NetComponentSystem.cs)细节
  - Awake：初始化
    - 创建[KService](Assets/Scripts/Core/Network/KService.cs)：kcp服务，用于接受链接和请求
    - 注册ReadCallback回调函数为[OnRead]，用于接受请求消息
  - OnRead：收到消息反序列化，并调用相关handler
    - 反序列化收到的消息
    - 调用EventSystem.Invoke发布 [NetComponentOnRead]事件
      - EventSystem.Invoke会找到对应的消息处理器，并调用其处理消息，参考下面的[Realm Fiber](#realm-fiber)
  - [CreateRouterSession<a id="crsession"></a>](Assets/Scripts/Hotfix/Client/Demo/NetClient/Router/RouterHelper.cs): 创建session
    - 获取Router地址
    - 调用Create创建RouterSession
    - 向session添加 PingComponent、RouterCheckComponent
  - Create: 创建session
    - AddChildWithId：创建session Entity
      - 创建一个Entity作为session元数据
      - 设置ID 
      - 并设置其[Parent](Assets/Scripts/Core/Entity/Entity.cs)，用于周期性调度
    - 设置RemoteAddress
    - [AService.Create](Assets/Scripts/Core/Network/KService.cs)创建KChannel
      - 创建KChannel
      - 添加创建KChannel到 localConnChannels


- 网络流程示意图
![网络流程示意图](readme_imgs/02_Networks00.jpg)
网络组件中的Call和Send的区别是，Call会异步等待结果返回，Send发给消息队列后就返回调用方。

### 服务端登录处理流程
#### Realm Fiber
- 初始化: server初始化时创建Realm Fiber，会发布FiberInit事件，然后由[FiberInit_Realm](Assets/Scripts/Hotfix/Server/Demo/Realm/FiberInit_Realm.cs)完成后续的初始化
  - 添加一系列组件 MailBoxComponent、TimerComponent、CoroutineLockComponent、ProcessInnerSender、MessageSender
  - 添加[NetComponent](Assets/Scripts/Hotfix/Share/Module/Message/NetComponentSystem.cs)负责接收数据。
- C2R_Login 消息处理过程<a id="c2r_loginhandler"></a>： 由 [NetComponent](Assets/Scripts/Hotfix/Share/Module/Message/NetComponentSystem.cs) 接收数据，调用EventSystem.Invoke相关消息，找到实际的消息处理器
  - [NetComponentOnReadInvoker_Realm](Assets/Scripts/Hotfix/Server/Demo/Realm/NetComponentOnReadInvoker_Realm.cs)处理该消息
    - 调用[MessageSessionDispatcher.Handle](Assets/Scripts/Model/Share/Module/Message/MessageSessionDispatcher.cs)收到的消息
      - 根据 C2R_Login 消息类型找到对应的Handler，即 [C2R_LoginHandler](Assets/Scripts/Hotfix/Server/Demo/Realm/C2R_LoginHandler.cs)处理消息
        - 随机分配一个Gate
        - 向gate请求一个key,客户端可以拿着这个key连接gate
          - 创建 R2G_GetLoginKey 请求，并使用 [MessageSender.Call](Assets/Scripts/Hotfix/Server/Module/Message/MessageSenderSystem.cs) 发送给选中的Gate
            - 内部会判断是否同一进程，如果是则由ProcessInnerSender转送，否则发消息给[NetInner]再发给Gate
          - 等待gate返回的 G2R_GetLoginKey
        - 填写响应R2C_Login
        - 使用session发送R2C_Login响应（[HandleAsync, Line:98](Assets/Scripts/Model/Share/Module/Message/MessageSessionHandler.cs)）
        - 关闭session。

#### Gate Fiber
- 初始化: server初始化时创建Gate Fiber，会发布FiberInit事件，然后由[FiberInit_Gate](Assets/Scripts/Hotfix/Server/Demo/Gate/FiberInit_Gate.cs)完成后续的初始化
  - 添加一系列组件 MailBoxComponent、TimerComponent、CoroutineLockComponent、ProcessInnerSender、MessageSender、PlayerComponent、GateSessionKeyComponent、LocationProxyComponent、MessageLocationSenderComponent
  - 添加[NetComponent](Assets/Scripts/Hotfix/Share/Module/Message/NetComponentSystem.cs)负责数据接受。
- R2G_GetLoginKey 消息处理: 
  - 调用[MessageDispatcherInfo.Handle](Assets/Scripts/Core/World/Module/Actor/MessageDispatcher.cs)收到的消息
  - 根据 R2G_GetLoginKey 消息类型找到对应的Handler，即 [R2G_GetLoginKeyHandler](Assets/Scripts/Hotfix/Server/Demo/Gate/R2G_GetLoginKeyHandler.cs)处理获取登录Key请求
  - 随机生成一个登录key
  - 把key添加到GateSessionKeyComponent中
  - 设置response，并返回 。 [MessageHandler Line 81](Assets/Scripts/Hotfix/Share/Module/Actor/MessageHandler.cs)
- C2G_LoginGate 消息处理过程：由 NetComponent。<a id="c2g_logingatehandler"></a>
  - 根据 C2G_LoginGate 消息类型找到对应的Handler，即 [C2G_LoginGateHandler](Assets/Scripts/Hotfix/Server/Demo/Gate/C2G_LoginGateHandler.cs)处理消息
    - 如果用户是登录，则创建用户信息和session
      - 向[PlayerComponent]中添加用户账号
      - 向[PlayerComponent]中添加[Player]
      - 设置[Palyer]的PlayerSessionComponent
      - 添加位置信息
      - session中添加[SessionPlayerComponent]
    - 如果用户是重连且在战斗中，发起用户重连异步处理任务: [CheckRoom]
      - 等待一个用户帧完成
      - 创建[G2Room_Reconnect](#g2r_reconnect)请求，并发送给房间管理纤程，并等待返回结果 Room2G_Reconnect
      - 创建[G2C_Reconnect](#g2c_reconnect)请求，路由StartTime、AuthorityFrame、所有玩家信息 给客户端
    - 设置[G2C_LoginGate].PlayerId
    - 调用session.Send(response), （[HandleAsync, Line:98](Assets/Scripts/Model/Share/Module/Message/MessageSessionHandler.cs)）


## ET状态同步
### 进入战斗流程
概述： 客户端登录完成后，创建进入战斗UI，点击进入时发送进入地图请求给Gate，Gate加载用户信息，并把相关信息转送给Map服务，Map服务控制用户地图加载以及角色创建。
![进入战斗流程图](readme_imgs/03_StateSync00.jpg)

#### 客户端
- 创建进入战斗UI： [登录](#登录流程)完成后会发布[**LoginFinish**]事件，状态同步处理该事件的Handler是[LoginFinish_CreateLobbyUI](Assets/Scripts/HotfixView/Client/Demo/UI/UILobby/LoginFinish_CreateLobbyUI.cs)
  - 调用[UIHelper.Create](Assets/Scripts/HotfixView/Client/Demo/UI/UIHelper.cs)创建 UILobby
    - 调用[UIComponent.Create](Assets/Scripts/HotfixView/Client/Module/UI/UIComponentSystem.cs)
      - 调用[UIGlobalComponent.OnCreate](Assets/Scripts/HotfixView/Client/Module/UI/UIGlobalComponentSystem.cs)
        - 根据UIType找到实际的AUIEventHandler为UILobbyEvent， 调用[UILobbyEvent.OnCreate](Assets/Scripts/HotfixView/Client/Demo/UI/UILobby/UILobbyEvent.cs)创建 UILobby
          - ResourcesLoaderComponent 加载UI资源，并创建UI
          - 添加 [UILobbyComponent](Assets/Scripts/HotfixView/Client/Demo/UI/UILobby/UILobbyComponentSystem.cs)绑定进入战斗按钮回调函数为[EnterMap]
            - [EnterMap]执行逻辑是调用 EnterMapHelper.EnterMapAsync
- 进入地图：[EnterMapHelper.EnterMapAsync](Assets/Scripts/Hotfix/Client/Demo/Main/Login/EnterMapHelper.cs)
  - 创建 C2G_EnterMap 请求
  - 等待[ClientSenderComponent.Call<a id="ClientSenderComponent-Call"></a>](Assets/Scripts/Hotfix/Client/Demo/Main/ClientSenderComponentSystem.cs)发送 C2G_EnterMap 请求返回的 G2C_EnterMap
    - 创建 A2NetClient_Request 请求，并设置 MessageObject = C2G_EnterMap 
    - 调用 ProcessInnerSender 发送请求给NetClient，进而发给服务器，参考[登录网络细节](#登录网络处理细节)
      - **区别**是[A2NetClient_RequestHandler](Assets/Scripts/Hotfix/Client/Demo/NetClient/A2NetClient_RequestHandler.cs)处理网络消息的发送。
  - await [Wait_SceneChangeFinish] 场景切换完成通知 
  - 发布 EnterMapFinish 事件
- 场景切换消息处理: [M2C_StartSceneChangeHandler](Assets/Scripts/Hotfix/Client/Demo/Main/Scene/M2C_StartSceneChangeHandler.cs) 
  - await [SceneChangeHelper.SceneChangeTo](Assets/Scripts/Hotfix/Client/Demo/Main/Scene/SceneChangeHelper.cs) 完成
    - 移除 AIComponent 
    - 获取 CurrentScenesComponent 组件
    - 当前场景 Dispose
    - 根据场景名称创建新场景： [CurrentSceneFactory.Create](Assets/Scripts/Hotfix/Client/Demo/Main/Scene/CurrentSceneFactory.cs)
    - 当前场景添加 UnitComponent
    - 发布 SceneChangeStart 事件 -->> [SceneChangeStart_AddComponent](Assets/Scripts/HotfixView/Client/Demo/Scene/SceneChangeStart_AddComponent.cs)
      - 加载场景地图
      - 添加 [OperaComponent] 
    - 等待 Wait_CreateMyUnit 事件到来
    - 使用[客户端UnitFactory.Create](Assets/Scripts/Hotfix/Client/Demo/Main/Unit/UnitFactory.cs)创建Unit
      - 获取当前场景的UnitComponent
      - 向UnitComponent添加Unit，并设置服务器传输的unit相关数据
      - 添加组件NumericComponent、MoveComponent，并同步设置服务器传输的组件信息
      - 添加ObjectWait、XunLuoPathComponent
      - 发布 AfterUnitCreate 事件：-->> [AfterUnitCreate_CreateUnitView](Assets/Scripts/HotfixView/Client/Demo/Unit/AfterUnitCreate_CreateUnitView.cs)处理，用于创建游戏角色
        - 加载Unit.prefab资源
        - 初始化Unit.prefab
        - 添加 GameObjectComponent、AnimatorComponent
    - 发布 SceneChangeFinish 事件
    - 通知 Wait_SceneChangeFinish
- 创建用户角色消息处理：[M2C_CreateMyUnitHandler](Assets/Scripts/Hotfix/Client/Demo/Main/Unit/M2C_CreateMyUnitHandler.cs) （为什么还有有这个过程？不解，场景切换时直接创建用户角色就可以了？为了展示异步用法？）
  - 通知场景切换协程继续往下走 ： 发布 Wait_CreateMyUnit 通知


---


#### 服务端
- Gate [C2G_EnterMapHandler](Assets/Scripts/Hotfix/Server/Demo/Gate/C2G_EnterMapHandler.cs)
  - 在Gate上动态创建一个Map Scene，把Unit从DB中加载放进来，然后传送到真正的Map中，这样登陆跟传送的逻辑就完全一样了
    - 创建GateMap Sence
    - sence添加 UnitComponent、 AOIManagerComponent、RoomManagerComponent、MailBoxComponent
  - 使用[服务端UnitFactory.Create](Assets/Scripts/Hotfix/Server/Demo/Map/Unit/UnitFactory.cs)创建Unit （实际服务应该从DB中加载用户数据）
    - 向 UnitComponent 添加一个 Unit(玩家角色的数据)
    - unit添加 MoveComponent、NumericComponent
    - 设置 unit 的位置，速度，AOI范围
    - unit加入AOIEntity
  - 创建用户进入地图配置，默认Map1
  - 启动协程执行[异步传输](Assets/Scripts/Hotfix/Server/Demo/Map/Transfer/TransferHelper.cs): 等到一帧后传送地图到Map Fiber
    - 等待纤程一帧结束：添加ETTask并异步等待，Fiber在LateUpdate中会设置ETTask结束等待。
    - 等待 TransferHelper.Transfer 完成传输
      - 创建 M2M_UnitTransferRequest 请求，并序列化unit
      - 序列化unit的所有组件
      - 定位中心 LocationProxyComponent 加锁
      - 等待传输任务结束：发送请求给Map Fiber，等待 M2M_UnitTransferRequestHandler 结束。
  - 返回G2C_EnterMap响应（该响应比地图传输早返回， [MessageSessionHandler Line:98](Assets/Scripts/Model/Share/Module/Message/MessageSessionHandler.cs) 调用 seesion.Send）
- Map 
  - [M2M_UnitTransferRequestHandler](Assets/Scripts/Hotfix/Server/Demo/Map/Transfer/M2M_UnitTransferRequestHandler.cs) 传输用户地图
    - 反序列化unit及其组件
    - 添加 MoveComponent、PathfindingComponent、MailBoxComponent
    - 通知客户端开始切场景
      - 创建 M2C_StartSceneChange 请求
      - 调用MapMessageHelper.SendToClient 发送请求
    - 通知客户端创建My Unit（为什么还有有这个过程？不解）
      - 创建 M2C_CreateMyUnit 请求
      - 调用MapMessageHelper.SendToClient 发送请求
    - 加入 AOIEntity 
    - 解锁location，可以接收发给Unit的消息

### 角色操控流程
![操控流程图](readme_imgs/03_StateSync10.jpg)
#### Client
[OperaComponent](Assets/Scripts/HotfixView/Client/Demo/Opera/OperaComponentSystem.cs) 用于角色移动控制
- 创建 C2M_PathfindingResult 请求
- [ClientSenderComponent.Send](Assets/Scripts/Hotfix/Client/Demo/Main/ClientSenderComponentSystem.cs)发送请求，流程参考[登录网络细节](#登录网络处理细节)

[M2C_StopHandler](Assets/Scripts/Hotfix/Client/Demo/Main/Move/M2C_StopHandler.cs)角色停止消息处理
- 根据消息Id获取要停止的角色
- 获取角色的 MoveComponent 
- 调用 [moveComponent.Stop](#movecomponent)

[M2C_PathfindingResultHandler](Assets/Scripts/Hotfix/Client/Demo/Main/Move/M2C_PathfindingResultHandler.cs)处理寻路结果
- 根据消息Id获取要停止的角色
- 获取角色的 MoveComponent
- 调用 [moveComponent.MoveToAsync](#movecomponent)


---


#### Server: Map Fiber
##### [寻路消息处理](Assets/Scripts/Hotfix/Server/Demo/Map/Move/C2M_PathfindingResultHandler.cs)
- 调用 [unit.FindPathMoveToAsync](Assets/Scripts/Hotfix/Server/Demo/Map/Move/MoveHelper.cs)
  - 获取unit的速度
  - 速度小于0.01，[广播](#broadcast)停止[M2C_Stop]消息
  - 否则，[PathfindingComponent.Find](Assets/Scripts/Hotfix/Share/Module/Recast/PathfindingComponentSystem.cs)获取寻路结果
  - [广播](#broadcast)寻路结果[M2C_PathfindingResult]
  - 服务器本地[移动角色 MoveComponent.MoveToAsync](#movecomponent)

##### [广播消息 MapMessageHelper.Broadcast <a id="broadcast"></a>](Assets/Scripts/Hotfix/Server/Demo/Map/MapMessageHelper.cs)
- 获取可看到当前角色的所有玩家
- 从 MessageLocationSenderComponent 获取 MessageLocationSenderOneType：可以根据Entity.ID反查Actor位置，[原理： book5.5 Actor Location](../Book/5.5Actor%20Location-ZH.md)
- 使用[MessageLocationSenderOneType.Send](Assets/Scripts/Hotfix/Server/Module/ActorLocation/MessageLocationSenderComponentSystem.cs)向每个玩家发送消息
  - 使用定位组件[LocationProxyComponent]获取消息进程ID
  - 调用 [MessageSender.Send](Assets/Scripts/Hotfix/Server/Module/Message/MessageSenderSystem.cs)
    - 同一进程消息调用ProcessInnerSender.Send
    - 网络消息创建A2NetInner_Message，发给NetInner纤程
      - [A2NetInner_MessageHandler](Assets/Scripts/Hotfix/Server/Module/Message/A2NetInner_MessageHandler.cs) 调用  [ProcessOuterSender.Send](Assets/Scripts/Hotfix/Server/Module/Message/ProcessOuterSenderSystem.cs)
        - StartProcessConfigCategory获取进程启动信息
        - 获取Session
        - 使用Session发送消息
---
#### [MoveComponent](Assets/Scripts/Hotfix/Share/Module/Move/MoveComponentSystem.cs)
- 实现原理
  - 采用[定时器](Assets/Scripts/Core/Fiber/Module/Timer/TimerComponent.cs)每帧(update中)移动一个路径点，结束时清除定时器。
  - 移动路径点时设置[unit.Position](Assets/Scripts/Model/Share/Module/Unit/Unit.cs)，会发布 [ChangePosition] 事件
    - 客户端:-->> [ChangePosition_SyncGameObjectPos](Assets/Scripts/HotfixView/Client/Demo/Unit/ChangePosition_SyncGameObjectPos.cs)移动角色   GameObject.transform.position
    - 服务端:-->> [ChangePosition_NotifyAOI](Assets/Scripts/Hotfix/Server/Demo/Map/AOI/ChangePosition_NotifyAOI.cs)更新AOI范围
  - 移动时设置[unit.Rotation]，会发布[ChangeRotation]事件
    - 客户端:-->> [ChangeRotation_SyncGameObjectRotation](Assets/Scripts/HotfixView/Client/Demo/Unit/ChangeRotation_SyncGameObjectRotation.cs)旋转角色  GameObject.transform.rotation
- StartMove : 开始移动
  - 设置StartTime
  - SetNextTarget：更新下一个移动目标
  - 启动定时器
- MoveForward: 更新到当前时间点为止角色应该移动的位置和角度
  - 计算目前为止，角色没有移动的时间片
  - 循环，直到时间片不大于0
    - 超过移动一步需要的时间，则移动角色到下一个位置，设置角色旋转角度
    - 没有超过，计算位置插值(貌似没有起作用)，计算旋转角度插值
    - 如果已经移动到最后一个点，MoveFinish，并退出
    - SetNextTarget：更新下一个移动目标
- SetNextTarget: 更新下一个移动目标，辅助函数
  - 更新当前移动下标
  - 更新角色当前位置
  - 更新上次移动时间
  - 计算角色旋转插值
- Stop：停止移动
  - 如果在移动中，则MoveForward
  - 调用 MoveFinish 
- MoveToAsync: 启动移动到目标位置的异步任务
  - Stop:停止移动
  - 初始化部分变量
  - 发布 [MoveStart] 事件
  - StartMove
  - 等待 ETTask结束
  - 发布 [MoveStop] 事件
- MoveFinish
  - 清零相关变量
  - 移除计时器
  - 设置ETTask返回结果
- FlashTo
  - 直接设置unit.Position


## ET帧同步

### 进入场景流程
![帧同步进入场景流程图](readme_imgs/04_LockSync01.jpg)
调用流程参考[状态同步](#状态同步)，这里只列出关键的handler  
#### 客户端
- 创建UI,绑定登录处理 [LoginFinish] -> [LoginFinish_CreateUILSLobby] ->[UILSLobbyEvent] -> [UILSLobbyComponent.EnterMap] ->[EnterMapHelper.Match]
- 客户端匹配请求处理[EnterMapHelper.Match](Assets/Scripts/Hotfix/Client/Demo/Main/Login/EnterMapHelper.cs)
  - 创建 C2G_Match 请求
  - [ClientSenderComponent.Call](Assets/Scripts/Hotfix/Client/Demo/Main/ClientSenderComponentSystem.cs)发给Gate
- 匹配成功，切换场景[Match2G_NotifyMatchSuccessHandler](Assets/Scripts/Hotfix/Client/LockStep/G2C_ChangeSceneHandler.cs)
  - [LSSceneChangeHelper.SceneChangeTo](Assets/Scripts/Hotfix/Client/LockStep/LSSceneChangeHelper.cs)
    - 添加 Room 组件
    - 发布 LSSceneChangeStart 事件
      - --》LSSceneChangeStart_AddComponent （View层)
        - 添加 ResourcesLoaderComponent 
        - 添加 UIComponent 
        - 创建房间UI
        - 加载场景资源
    - 发送 [C2Room_ChangeSceneFinish](#c2r_csfinish) 请求
    - 等待 Wait_Room2C_Start 通知
    - 创建 LSWorld 
    - 本地 [Room.Init](#room_init)
    - 添加 [LSClientUpdater](#lsclientupdatersystem)： **客户端帧同步处理组件**
    - 发布 LSSceneInitFinish 事件
      - --》LSSceneInitFinish_Finish （view层）
        - 添加并初始化玩家角色 [LSUnitViewComponent.InitAsync](Assets/Scripts/HotfixView/Client/LockStep/LSUnitViewComponentSystem.cs) 
          - 从room.LSWorld获取 LSUnitComponent
          - 为room中的每个玩家创建角色
            - lsUnitComponent中获取角色的元数据
            - ResourcesLoaderComponent加载预制件
            - 调用UnityEngine初始化角色GameObject
            - 设置角色的位置
            - 用LSUnitView包装角色GameObject，并添加到 LSUnitViewComponent
            - 为角色添加 LSAnimatorComponent
        - 添加 LSCameraComponent 、 LSOperaComponent
        - 移除 UILSLobby
- Room2C_Start 消息处理 [Room2C_EnterMapHandler](Assets/Scripts/Hotfix/Client/LockStep/Room2C_EnterMapHandler.cs)
  - 通知 Wait_Room2C_Start
- 重连 [G2C_ReconnectHandler<a id="g2c_reconnect"></a>](Assets/Scripts/Hotfix/Client/LockStep/G2C_ReconnectHandler.cs)
  - LSSceneChangeHelper.SceneChangeToReconnect：与 SceneChangeTo 流程大致相同，区别是不用再发 C2Room_ChangeSceneFinish 请求，也不用等待 Wait_Room2C_Start

#### 服务端
- Gate 
  - 路由匹配请求 [C2G_MatchHandler](Assets/Scripts/Hotfix/Server/LockStep/Gate/C2G_MatchHandler.cs)
    - 创建 [G2Match_Match] 请求
    - [MessageSender.Call] 异步等待 Match服务返回
  - 路由匹配通知[Match2G_NotifyMatchSuccessHandler](Assets/Scripts/Hotfix/Server/LockStep/Gate/Match2G_NotifyMatchSuccessHandler.cs)
    - 使用Session路由消息给Player
- Match 
  - 处理匹配[G2Match_MatchHandler](Assets/Scripts/Hotfix/Server/LockStep/Match/G2Match_MatchHandler.cs)
    - 启动 [MatchComponent.Match](Assets/Scripts/Hotfix/Server/LockStep/Match/MatchComponentSystem.cs) 协程
      - 人数不够直接返回
      - 申请一个房间
        - 创建 Match2Map_GetRoom 请求
        - 发送给 Map 服务并等待结果 Map2Match_GetRoom 
        - 通知所有玩家匹配成功 Match2G_NotifyMatchSuccess
    - 直接返回
- Map 
  - 创建匹配房间[Match2Map_GetRoomHandler](Assets/Scripts/Hotfix/Server/LockStep/Map/Match2Map_GetRoomHandler.cs)
    - 创建一个RoomRoot纤程 
    - 发 RoomManager2Room_Init 消息给 RoomRoot，并等待返回
    - 直接返回
- RoomRoot 
  - 房间初始化[RoomManager2Room_InitHandler](Assets/Scripts/Hotfix/Server/LockStep/Room/RoomManager2Room_InitHandler.cs)
    - 添加 [Room](#roomsystem) 组件
    - 添加 [RoomServerComponent](Assets/Scripts/Hotfix/Server/LockStep/Map/RoomServerComponentSystem.cs): 辅助Room创建？
    - 创建 LSWorld 
    - 直接返回
  - 客户端场景切换完成处理<a id="c2r_csfinish"></a>[C2Room_ChangeSceneFinishHandler](Assets/Scripts/Hotfix/Server/LockStep/Map/C2Room_ChangeSceneFinishHandler.cs)
    - 设置当前player进度100%
    - 如果不是所有玩家进度100%，返回
    - 创建 Room2C_Start 消息
    - 添加所有玩家信息
    - 服务端 [Room.Init](#room_init)
    - 添加 [LSServerUpdater](#lsserverupdatersystem): **服务端帧同步处理组件** 
    - 广播客户端
  - 重连 [G2Room_ReconnectHandler<a id="g2r_reconnect"></a>](Assets/Scripts/Hotfix/Server/LockStep/Room/G2Room_ReconnectHandler.cs)
    - 把StartTime、当前帧、所有玩家信息，发给Gate

### 帧同步逻辑
![帧同步逻辑](readme_imgs/04_LockSync10.jpg)  

#### 客户端
帧同步客户端更新：**[LSClientUpdaterSystem](Assets/Scripts/Hotfix/Client/LockStep/LSClientUpdaterSystem.cs)** <a id="lsclientupdatersystem"></a>
- Update
  - 若未到当前帧时间则退出
  - 最多只预测5帧，否则退出
  - 获取一帧: GetOneFrameMessages
    - 当前预测帧不早于服务器，直接返回frameBuffer.FrameInputs[frame]
    - 大于授权帧，则用授权帧+当前用户输入代表下一预测帧（估计是大部分时间用户输入为空，一般用户输入频率<=7FPS，这里的更新频率为20FPS）
  - 更新一帧: [room.Update](#room_update)
  - 发送hash数据: [room.SendHash](#room_sendhash)
  - 创建 [FrameMessage](#framemessagehandler)，并设置用户输入，客户端帧号
  - 发送给Room：ClientSenderComponent.Send  

[Room2C_AdjustUpdateTimeHandler](Assets/Scripts/Hotfix/Client/LockStep/Room2C_AdjustUpdateTimeHandler.cs)
- 调整帧更新时间，简单理解：客户端新的更新时间 = 服务更新频率/客户端更新频率 * 客户端更新时间

[Room2C_CheckHashFailHandler](Assets/Scripts/Hotfix/Client/LockStep/Room2C_CheckHashFailHandler.cs)
- 没看懂，看代码只是返解压了 TODO

玩家角色视图：[LSUnitView](Assets/Scripts/HotfixView/Client/LockStep/LSUnitViewSystem.cs)，即Unity GameObject的封装
- Update
  - 根据LSUnit元数据，更新成员变量 Position 、 Rotation等
  - 设置 LSAnimatorComponent 的 speed
  - 更新角色的位置、转向

---

#### 服务端
**[FrameMessageHandler](Assets/Scripts/Hotfix/Server/LockStep/Map/FrameMessageHandler.cs)** <a id="framemessagehandler"></a>
- 动态调帧率：每秒，更新一下客户端帧率
  - 计算客户端与服务端帧的时间差值
  - 发送更新帧率消息 Room2C_AdjustUpdateTime 给客户端
- 客户端帧小于服务端授权帧，退出
- 客户端帧大于服务端授权帧10帧，退出
- 记录当前用户输入到帧缓存中

**[LSServerUpdaterSystem](Assets/Scripts/Hotfix/Server/LockStep/Room/LSServerUpdaterSystem.cs)** <a id="lsserverupdatersystem"></a>
- 若未到当前帧时间则退出
- 获取一帧：GetOneFrameMessages
  - 从缓存池中取出对应帧，若所有用户输入都更新了，则返回该帧
  - 有人输入的消息没过来，给他使用上一帧的操作
- 创建 OneFrameInputs 消息
- 广播给所有玩家
- 更新一帧: [room.Update](#room_update)

[C2Room_CheckHashHandler](Assets/Scripts/Hotfix/Server/LockStep/Room/C2Room_CheckHashHandler.cs)
- 比较客户端hash与房间hash值，若不相等
  - 创建 Room2C_CheckHashFail 消息
  - copy服务端 LSWorldBytes 到消息内容中
  - 发送给客户端 MessageLocationSenderOneType.Send

---  
#### 公共逻辑  
房间：**[RoomSystem](Assets/Scripts/Hotfix/Share/LockStep/RoomSystem.cs)** <a id="roomsystem"></a>
- Init <a id="room_init"></a>
  - 初始化成员变量
    - 设置起始时间
    - 设置授权帧、预测帧帧号
  - 创建帧缓存 FrameBuffer 
  - 创建帧更新计时器 FixedTimeCounter
  - LSWord初始化
    - 设置起始帧号
    - 添加 LSUnitComponent
    - 对每个玩家进行初始化[LSUnitFactory.Init](Assets/Scripts/Hotfix/Share/LockStep/LSUnitFactory.cs)
      - 向 LSUnitComponent 添加  LSUnit（玩家元数据) 
      - 设置玩家位置和旋转
      - 玩家添加 [LSInputComponent](#lsinputcomponent)
- Update <a id="room_update"></a>   
  - 把当前帧所有玩家的输入设置到LSWorld中的玩家元数据上
  - 如果不是重播
    - 保存LSWorld
    - 记录LSWorld帧
  - 更新LSWorld: LSWorld.Update
- SendHash() <a id="room_sendhash"></a>: 扩展方法，client发送hash用于验证
  - 创建 C2Room_CheckHash 消息
  - ClientSenderComponent.Send发送

帧同步游戏世界：[LSWorld](Assets/Scripts/Model/Share/LockStep/LSWorld.cs)，保存了所有的玩家状态
- Update
  - 调用[LSUpdater.Update()](Assets/Scripts/Model/Share/LockStep/LSUpdater.cs)
    - 调用LSEntitySystemSingleton.Instance.[LSUpdate](Assets/Scripts/Model/Share/LockStep/LSEntitySystemSingleton.cs);
      - 内部实现，调用lsEntity的LSUpdate()方法，目前只有一个 LSInputComponent

角色元数据位置更新组件：[LSInputComponent<a id="lsinputcomponent"></a>](Assets/Scripts/Hotfix/Share/LockStep/LSInputComponentSystem.cs)
- LSUpdate
  - 从父节点获取 LSUnit
  - 计算位移
  - 更新unit位置
  - 更新unit朝向


  
## 个人评价
### ET优点
概括为ECS、事件、异步。  
- 组件+事件通知，模块独立，降低耦合。
- ET中采用Attribute自动注册简化了注册流程和时机
- 异步任务，使得并发和代码编写更简单。
- 封装了网络消息的收发过程，变成了事件处理过程。
- 网络通讯采用单独通讯线程+消息队列机制是比较好的做法，底层采用了KCP通讯，也是一个亮点。

### ET缺点 
感觉读项目代码比较难受，绕来绕去，估计是为了Demo其核心机制? 剩余其他的都算是小缺陷，以下列举一些。
- 用c#的await+Task+SynchronizationContext很容易实现Fiber和异步编程，目前还没想明白为啥ET中重新实现了一遍。多用c#中的Event、Action、Func等机制，应该能更简洁的实现ET中的功能。
- 感觉事件的实现，改用显示的注册(+=)和取消（-=），更容易追踪代码，且也能自动实现。
- ET中的很多流程和事件处理，调用和转发了太多层
  - 消息的收发，经过多层才最终发送消息，收发消息都是在update中执行的，会延迟3ms；或许可以改成：消息直接发到消息队列，收发线程轮询处理消息收发，收到消息放消息队列or触发事件回调。
  - UI创建层层转发，感觉可以用UIFactor直接创建。

ET整体设计理念是非常优秀的了，深以为然，世界上并没有完美的项目，以上说的痛点并非致命的缺陷，采用一种设计模式，必然有利有弊。  
实现上有些改进空间，但不是必须的，先把要做的游戏实现，进行商业试水，跑完流程才是当务之急。
提出这些疑问，只是个人理解别人框架，与自己知识体系碰撞融合的一个过程。

- P0: 先实现游戏，走完商业流程。 
- P2: 实现EasyGame平台，流水线开发游戏。
  - 简单易用：
    - 可视化编程，不用代码也能开发游戏；
    - 程序员友好，方便定制
    - 提供常见的组件： 包括常用的skill和Buff管理，通讯，特效，热更新，AI，渲染
  - 高效：
    - 自动3D建模：场景，角色，特效，音效等。游戏大模型。
    - 可自动完成大部分编程，部署和运营