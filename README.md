# CustomVisionSample
It's an end to end C# sample from creating a project in custom vision till exporting confusion matrix in CSV format.

## Pre-requisites
You'll need to install [Visual Studio](https://www.visualstudio.com/vs/community/) to build and run this code. 

## Steps to run the sample
1. Sign in or create a new account on [customvision.ai](https://www.visualstudio.com/vs/community/)
2. After login - click on cog icon on the right hand side and copy key values into the appropriate app settings

    <img src="https://github.com/Azadehkhojandi/CustomVisionSample/blob/master/documents/CustomVision1.PNG?raw=true" width="900"/>
    
3. Register for the free [Bing Search service](https://azure.microsoft.com/try/cognitive-services/?api=bing-web-search-api) then copy the key value into the app settings

    <img src="https://raw.githubusercontent.com/Azadehkhojandi/CustomVisionSample/master/documents/BingSearch.PNG" width="900"/>

## Running the application

The app will create a project in your custom vision dashboard with a random GUID. Please note that each time you run the app, a new project will be created. Next the app reads all the tags in the `tags.csv` file. The system searches Bing Images for each tag to prepare the source set of images for the classifier. 

 <img src="https://raw.githubusercontent.com/Azadehkhojandi/CustomVisionSample/master/documents/CustomVision2.PNG" width="900"/>

By default the downloads eight images for each tag - five images for training the model and three images for the testing the model.
After downloading the images, the app will then upload the photos into the customvision.ai project and tag them accordingly.

Now the model has enough data to be trained. The app will train the model and set the default iteration to the newly trained model.

The model is ready for testing so the app will try to test the model the results of which are  exported as a confusion matrix into `result.csv`.
You can check the quality of your model by reviewing the [confusion matrix](https://en.wikipedia.org/wiki/Confusion_matrix).

As you are running the app you should see output similar to this animation. 
![process running](https://user-images.githubusercontent.com/5225782/34922153-2eba3654-f9e0-11e7-9d50-d11950e35c71.gif)


##  More information on the Azure Custom Vision Service

- [Start here](https://docs.microsoft.com/en-au/azure/cognitive-services/custom-vision-service/home)
- [Custom Vision API C# Tutorial](https://docs.microsoft.com/en-au/azure/cognitive-services/custom-vision-service/csharp-tutorial)
