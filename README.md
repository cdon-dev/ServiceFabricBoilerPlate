# ServiceFabricBoilerplate

## Setup

When setting up a new Service Fabric Application project using the VS template, we use the naming convetion "ServiceFabric". Then we do a couple of changes to the genereated default.

In a web project we change *launchSettings.json* to the post is the same in both IIS and Web Project.
Next, we set the same port in the *ServiceManifest.xml*.

In the service *proj file we set the following;

        <AssemblyName>XXX</AssemblyName>
        <RootNamespace>XXX</RootNamespace>

