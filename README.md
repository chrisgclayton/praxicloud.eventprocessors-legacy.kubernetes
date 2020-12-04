# PraxiCloud Event Processors - Legacy Kubernetes
PraxiCloud Libraries are a set of common utilities and tools for general software development that simplify common development efforts for software development. The event processors legacy library contains lease and checkpoint managers to be used with the legacy event processor host.



# Installing via NuGet

Install-Package PraxiCloud-EventProcessors-Legacy-Kubernetes



# Event Processor for Kubernetes



## Key Types and Interfaces

|Class| Description | Notes |
| ------------- | ------------- | ------------- |
| **LegacyProcessorBase** | This is the base class for Legacy Event Processor Host containers, implementing many of the common features of a Kubernetes pod. <br />***IsContainerHealthy*** override this to control responses for the health probe.<br />***IsContainerAvailable*** override this to control responses for the availability probe. This will be set to true at the beginning of the execution loop and false at the end. Control over this may be done as an addition to these.<br />***HandleScaleEventAsync*** is invoked when a scale event occurs and the desired replica count has changed, reducing the "noise" of pod change of state.<br />***StartAsync*** can be overridden to introduce additional startup logic.<br />***ShutdownAsync*** can be overridden to introduce additional shutdown logic.<br />***UnhandledExceptionRaised*** is invoked when an application domain or thread pool exception is raised and not caught. The default behavior is to log and allow it to terminate if required.<br />***GetEventHubConnectionStringAsync***<br />Returns the connection string for the target Event Hub endpoint.***GetStorageConnectionStringAsync***<br />***CreateEventProcessor*** Returns the connection string for the Azure Storage account used to checkpoint and manage the epoch. | This is provided as a base for extended use cases with the generic version being recommended when possible. |
| **LegacyProcessorBase<T>** | This is a specialized class of the Legacy Processor where the type of the event processor is provided for easy creation. | The processor must accept a public constructor with the following parameters (PartitionContext partitionContext, ILoggerFactory loggerFactory, IMetricFactory metricFactory). |
| **ProcessorUtilities** | A set of helper utilities that perform common operations associated with the legacy processor.<br />***GetStartPosition*** Gets the stream processing start position identifier based on environment variables. StartPosition indicates the type of start position such as Start, End, Offset (looks also at StartOffset), Sequence (looks also at StartSequence) and Time (looks at StartSeconds to determine time in past).<br />***GetProbeConfiguration*** Gets a probe configuration based on the environment variables. UseTcpProbe is true if TCP or false for HTTP. ReadinessProbePort identifies the port the availability probe should listen on. HealthProbePort identifies the port the health probe should listen on.<br />***GetEventHubConnectionStringAsync*** Retrieves the Event Hub connection string from a secret in Key Vault based on the environment variables. HubSecretName is the name of the secret in Key Vault. <br />***GetStorageConnectionStringAsync*** Retrieves the Storage connection string from a secret in Key Vault based on the environment variables. StorageSecretName is the name of the secret in Key Vault.<br />All Key Vault access operations leverage the KeyVaultName, IdentityClientId, TenantId, ClientId and ClientSecret variables for the vault and service principal to connect as. If TenantId, ClientId and ClientSecret is not provided it uses managed identity. | A probe port value of 0 will disable it. |


## Additional Information

For additional information the Visual Studio generated documentation found [here](./documents/praxicloud.eventprocessors-legacy.kubernetes.xml), can be viewed using your favorite documentation viewer.



# PraxiCloud Event Processors - Legacy Kubernetes Sample

The sample container demonstrates the use of the the event processor host, with the legacy framework. This is done as a basic implementation of the LegacyProcessorBase<T>. There is a docker file to build the solution in the root and a deployment directory that includes YAML files for Kubernetes deployment. The YAML files have placeholders that need populated with the appropriate values for the deployment environment.






