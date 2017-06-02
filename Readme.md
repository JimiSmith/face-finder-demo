### Face Finder

A simple API running on Azure Functions

This API accepts an image containing faces, stores it on Azure Blob Storage and submits the image to Azure Cognitive Services.

### Setup

You'll need Visual Studio 2017 Preview (or later) and the latest Azure Functions tooling installed to run this project.

There is an [accompanying blog post](https://blog.jimismith.me/blogs/a-simple-api-using-azure-functions-1) for this repo that contains more details on how to get this running

### Local Settings

You'll need to create a file in `FunctionsFaceDemo` named `local.settings.json` with the following content

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "YourStorageConnectionString",
    "AzureWebJobsDashboard": "YourStorageConnectionString",
    "CognitiveServicesHost": "YourCognitiveServicesHost",
    "CognitiveServicesApiKey": "YourCognitiveServicesKey"
  }
}
```

### Publishing

1) Create a functions app in the Azure Portal
2) You'll need to add the settings in `local.settings.json` to the application settings in the Azure portal
3) Right click the project in Visual Studio and select Publish
4) Follow the instructions