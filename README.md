# Unity3D.Amqp
**AMQP client library for Unity 3D supporting RabbitMQ**

This is a Unity 3D package and set of source files that allows the ability to use [AMQP](http://www.amqp.org/) within Unity 3D projects. For now the initial support will be solely based around the [.NET RabbitMQ client](http://www.rabbitmq.com/dotnet.html).

Attempts to track down other integrations of AMQP clients into Unity 3D all came up short so this project was created to provide a straight-forward integration. Trying to integrate the official .NET RabbitMQ client proved to be rather cumbersome and had several large roadblocks. This project exists in a state where it is possible to compile a working version of the RabbitMQ client library and a Unity asset package that can be imported into other Unity projects.

**Note:** This project is very new so the documentation will probably be a bit sparse until there is more time to build it up. The project will also be constantly adding features and may including breaking changes. The code is fairly well documented however so if you feel like just diving in and trying to make sense of it - go for it! It will still be easier than trying from scratch to get AMQP support in Unity.

This project offers the following:
* A Unity asset package that can be imported into Unity projects made with Unity 5.x+ that provides easy AMQP client integration (tested with Windows and MacOS builds so far, Linux will likely work too)
* A custom .NET library that wraps the RabbitMQ client and provides extensibility for integrating other AMQP clients beyond RabbitMQ .NET if necessary
* A thread-safe pattern that plays nice with Unity's game thread (out of the box the .NET RabbitMQ client cannot directly interact with Unity's game thread)
* Unity C# source files that provide Unity-specific classes and MonoBehaviour scripts for working with AMQP including a useful diagnostics console
* A working version of the .NET RabbitMQ client version 3.4.4 that builds with Visual Studio 2015 (this is actually important, read on to find out why)

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Thread Safety](#thread-safety)
- [Compatibility](#compatibility)
  - [.NET RabbitMQ client 3.4.4](#net-rabbitmq-client-344)
  - [Build Target Support](#unity-3d-build-targets)
- [License](#license)

## Installation

Currently you should build locally by downloading the source or cloning the repository. To build, you will need [Visual Studio 2015](https://www.visualstudio.com/downloads/). Building with Mono might be possible with additional steps but that hasn't been attempted yet.

**Note:** If you don't need to build the project from source you can just use the asset package directly in your Unity project. See the [Quick Start](#quick-start) section for details.

## Quick Start

The fastest way to get up and running with AMQP in your Unity project is to just import the asset package. You can find the asset package in this project here: **/unity/CymaticLabsUnityAmqp.unitypackage**.

**Note:** If you don't want to download the whole project and just want to work with the asset package you can download it from the [releases section](https://github.com/CymaticLabs/Unity3D.Amqp/releases) of the project.

For an example of how to work with the library, open the **AmqpDemo** scene found at **/Assets/CymaticLabs/Amqp/Scenes/** in the Unity project you imported it into.

Find the **AmqpClient** game object in the demo scene and familiarize yourself with the **AmqpClient.cs** script. This MonoBehaviour is the main way to interface with an AMQP broker from Unity. It exposes events and methods for connecting, disconnecting, subscribing, and publishing to an AMQP broker. If you only need a single broker connection it has convenient static methods or you can create multiple instances for multiple connections.

## Thread Safety

Unity's main game thread cannot directly interact with the thread(s) that the AMQP messaging functions occur on. If you try to call UnityEngine code directly from events that occur in the underlying AMQP client you will likely experience unhandled exceptions telling you such interactions are not allowed. The **AmqpClient** class (which subclasses MonoBehaviour) implements a thread-safe pattern. Within the **AmqpClient** class, client connection events (such as connect, disconnect, reconnect, blocked, subscribed, etc.) and received message events are quickly queued in thread-safe variables and then processed on the **Update()** method of **AmqpClient** to allow safe interaction with Unity's main game thread. Be aware of this limitation when working with this library.

## Compatibility

### .NET RabbitMQ client 3.4.4

Quite a few issues exist when attempting to integrate AMQP with Unity, specifically with the official .NET RabbitMQ client. Since Unity  (as of version 5.5.1) still only supports .NET Framework 3.5 that means that a specific version of the .NET RabbitMQ client must be used. The last version to support .NET 3.5 is version 3.4.4 of the RabbitMQ client. Any version greather than 3.4.4 is not compatible with Unity.

Building version 3.4.4 has a few issues. It's an archived version at this point so first you must track it down in the official git repository for the client. Also it was built with Visual Studio 2008 and when opening the project in Visual Studio 2015 the upgrade fails with errors. The [build instructions](http://www.rabbitmq.com/build-dotnet-client.html) on RabbitMQ's offical website are no longer accurate to version 3.4.4 and it was difficult to track them down. The [internet archive](https://archive.org/) fortunately yielded a page with build instructions.

The version of the RabbitMQ client library that is in the **/lib** folder of this project has already been upgraded and will open and build properly in Visual Studio 2015.

The **CymaticLabs.Unity3D.Amqp** project references the local 3.4.4 client projects directly. This is useful if you need to debug the RabbitMQ client library itself (including break points). Also there seems to be no official nuget package for 3.4.4, only 3.4.3 and 3.5.0; so this project includes the additional bug fixes of 3.4.4.

### Unity 3D Build Targets

Presently this project has only been tested against Windows and MacOS (and not all versions). Since the MacOS Mono version works, it is likely it will also work for Linux builds.

Android and iOS support are definitely on the roadmap. Hopefully things will work as-is, but if not there at least seems like some alternative routes to get things working. For Android [this github project](https://github.com/codeMonkeyWang/Unity-RabbitMQ) provides some clues. [This post about Xamarin integration](https://forums.xamarin.com/discussion/49858/using-rabbitmq-amqp-with-xamarin-forms) also seems to provide some clues and makes support seem likely.

## License

Code and documentation are available according to the *MIT* License (see [LICENSE](https://github.com/CymaticLabs/Unity3D.Amqp/blob/master/LICENSE)).
