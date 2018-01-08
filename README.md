# CustomVisionSample
It's an end 2 end c# sample from creating a project in custom vision till exporting confusion matrix in CSV format

Steps to run the sample
1- sign in or first create a new account -   https://azure.microsoft.com/en-au/free/ 
2- after login - click on cog sign on the right-hand side and copy key values into app settings
https://github.com/Azadehkhojandi/CustomVisionSample/blob/master/documents/CustomVision1.PNG?raw=true


![alt text](https://raw.githubusercontent.com/Azadehkhojandi/CustomVisionSample/master/documents/CustomVision1.PNG)


3- register for free bing search service - https://azure.microsoft.com/try/cognitive-services/?api=bing-web-search-api and copy the key value into the app settings

run the application
App creates a project in your custom vision dashboard.
Then, it reads all the tags in the tags.csv file and downloads 8 images for each tag. 
5 images for training the model and 3 images for the testing the model.
After downloading the images, the app will upload the photos into the project and tag them accordingly.
Now the model has enough data to be trained. the app will train the model and set the default iteration to the newly trained model.
our model is ready for testing, the app will try to test the model and exports confusion matrix into result.csv.
you can check how good or bad your model is.


more information about custom vision - https://docs.microsoft.com/en-au/azure/cognitive-services/custom-vision-service/home
