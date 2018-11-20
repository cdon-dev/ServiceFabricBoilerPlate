# ServiceFabricBoilerplate


### ServiceFabricExecution

#### RunAsync
When [RunAsync] (https://docs.microsoft.com/en-us/dotnet/api/microsoft.servicefabric.services.runtime.statelessservice.runasync?view=azure-dotnet) is executed the cancelationtoken must be honored for graceful shutdown, and exceptions that escape the function tells the run time different things. To ease usage the common exception handling is wrapped in a utility function under ServiceFabricExecution.RunAsync. 

#### WhileAsync
It's common that you'll do some processing or continues work in RunAsync, introducing some kind of while loop. ServiceFabricExcution.WhileAsync is a utility function for creating this kind of loop, with execption handling and logging.

		protected override Task RunAsync(CancellationToken cancellationToken)
			=> ServiceFabricExecution.RunAsync(ct => ServiceFabricExecution.WhileAsync(token => DoWorkAsync(token), "DoWork", ct, logger), logger);

