using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ConstumVisionEnd2End.Models;
using ConstumVisionEnd2End.Services;
using CsvHelper;
using Microsoft.Cognitive.CustomVision.Prediction;
using Microsoft.Cognitive.CustomVision.Training;
using Microsoft.Cognitive.CustomVision.Training.Models;

namespace ConstumVisionEnd2End
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                MainAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }

        }

        static async Task MainAsync(string[] args)
        {
            /* you need at least 2 tags and 5 images for each tag to start*/

            var basepath = Directory.GetCurrentDirectory();

            //read tags
            var tsgfilepath = $"{basepath}\\tags.csv";
            Console.WriteLine($"\tReading Tags from {tsgfilepath}");
            var tags = ReadTags(tsgfilepath);


            //generate random name for the project 
            var projectName = Guid.NewGuid().ToString();
            Console.WriteLine($"\tCreating a project in customvision.ai - project name: {projectName}");
            var project = CreateProject(projectName);


            //download images - 
            var trainingSetPath = $"{basepath}\\{projectName}\\data\\TrainingSet";
            var testSetPath = $"{basepath}\\{projectName}\\data\\TestSet";
            Console.WriteLine($"\tBing search & downloading images - split them in TrainingSet & TestSet for each tag");
            Console.WriteLine($"\tTrainingSetPath: {trainingSetPath}");
            Console.WriteLine($"\tTestSetPath: {testSetPath}");

            using (var bingImageSearchService = new BingImageSearchService())
            {
                foreach (var tag in tags)
                {
                    Console.WriteLine($"\tStart Process for : {tag}");
                    var bingresult = await bingImageSearchService.ImageSearch(tag, 20);
                    if (bingresult.value != null)
                    {
                        //training 
                        DownloadImages($"{trainingSetPath}\\{tag}", bingresult.value.Take(10).ToList(),5);
                        //test
                        DownloadImages($"{testSetPath}\\{tag}", bingresult.value.Skip(10).ToList(),3);
                    }
                }
            }


            CreateTheModel(trainingSetPath, project);

            // Now there are images with tags start training the project
            TrainTheModel(project);

            Console.WriteLine($"\tTesting the Model");

            TestingTheModel(testSetPath, project);

            Console.WriteLine($"\tPress any key to exit");
            Console.ReadKey();
        }

        private static List<string> ReadTags(string tsgfilepath)
        {

            try
            {
                var result = new List<string>();
                using (TextReader reader = File.OpenText(tsgfilepath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        result.Add(line);
                    }
                    return result;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

        }

        private static void DownloadImages(string pathString, List<Value> items, int numberofimagestodownload = -1)
        {
            var counter = 0;
            Directory.CreateDirectory(pathString);
            foreach (var item in items)
            {
                if (numberofimagestodownload > 0 && counter > numberofimagestodownload)
                {
                    return; 
                }
                try
                {

                    var imageurl = item.contentUrl;
                    var webClient = new WebClient();
                    var uri = new Uri(imageurl);
                    var filename = uri.Segments.Last();
                    webClient.DownloadFile(imageurl, pathString + "\\" + filename);
                    counter = counter + 1;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        private static void TestingTheModel(string testingSetPath, Project project)
        {
            var predictionKey = ConfigurationManager.AppSettings["CustomVision_PredictionKey"];
            var predictionEndpoint = new PredictionEndpoint() { ApiKey = predictionKey };

            var testModel = new List<Model>();

            var testSet = Directory.GetDirectories(testingSetPath);
            var predictionResult = new Dictionary<string, int>();
            var labels = new List<string>();

            foreach (var subdirectory in testSet)
            {
                var images = Directory.GetFiles($"{subdirectory}").Select(f =>
                {
                    testModel.Add(new Model() { Label = subdirectory, Path = f });
                    return new MemoryStream(File.ReadAllBytes(f));
                }).ToList();
                foreach (var testImage in images)
                {
                    var dir = new DirectoryInfo(subdirectory);
                    var label = dir.Name;
                    labels.Add(label);

                    Console.WriteLine($"\tActucal tag: {label}");

                    var result = predictionEndpoint.PredictImage(project.Id, testImage);
                    var predictedClass = result.Predictions[0].Tag;
                    var predictedProb = result.Predictions[0].Probability;

                    var key = $"{label}|{predictedClass}";
                    if (!predictionResult.ContainsKey(key))
                    {
                        predictionResult.Add(key, 0);
                    }
                    predictionResult[key] = predictionResult[key] + 1;

                    // Loop over each prediction and write out the results
                    foreach (var c in result.Predictions)
                    {
                        Console.WriteLine($"\t{c.Tag}: {c.Probability:P1}");
                    }
                }
            }

            using (TextWriter writer = new StreamWriter($"{testingSetPath}\\testModel.csv"))
            {
                var csv = new CsvWriter(writer);

                csv.WriteRecords(testModel);
            }






            labels = labels.Distinct().ToList();

            // Two-dimensional array. [row,col]
            var array2D = new string[labels.Count + 1, labels.Count + 1];

            var colindex = 0;
            var rowindex = 0;
            foreach (var lable in labels)
            {
                array2D[rowindex, colindex] = lable;
                rowindex = rowindex + 1;
            }

            colindex = 1;
            rowindex = labels.Count;
            foreach (var label in labels)
            {
                array2D[rowindex, colindex] = label;
                colindex = colindex + 1;
            }


            rowindex = 0;

            foreach (var trueLabel in labels)
            {
                colindex = 1;
                foreach (var predictedLabel in labels)
                {
                    var key = $"{trueLabel}|{predictedLabel}";
                    var value = 0;
                    if (predictionResult.ContainsKey(key))
                    {
                        value = predictionResult[key];

                    }
                    array2D[rowindex, colindex] = value.ToString();
                    colindex = colindex + 1;
                }
                rowindex = rowindex + 1;
            }

            //pretty print

            PrettyPrint(array2D);


            ExporttoCSV(testingSetPath, array2D);
        }

        private static void ExporttoCSV(string testingSetPath, string[,] array2D)
        {
            var rowLength = array2D.GetLength(1);
            var colLength = array2D.GetLength(0);
            //csv file
            using (var file =
                new StreamWriter($"{testingSetPath}\\result.csv"))
            {
                for (var i = 0; i < rowLength; i++)
                {
                    var item = new List<string>();
                    for (var j = 0; j < colLength; j++)
                    {
                        item.Add(array2D[i, j]);
                    }
                    file.WriteLine(string.Join(",", item));
                }
            }
        }

        private static void PrettyPrint(string[,] array2D)
        {
            var rowLength = array2D.GetLength(1);
            var colLength = array2D.GetLength(0);

            Console.WriteLine("-------------------------------");


            for (var i = 0; i < rowLength; i++)
            {
                for (var j = 0; j < colLength; j++)
                {
                    Console.Write($"{array2D[i, j],-30}");
                }
                Console.Write(Environment.NewLine + Environment.NewLine);
            }
        }

        private static void TrainTheModel(Project project)
        {
            var trainingKey = ConfigurationManager.AppSettings["CustomVision_TrainingKey"];
            var trainingApi = new TrainingApi() { ApiKey = trainingKey };

            var iteration = trainingApi.TrainProject(project.Id);

            Console.WriteLine($"\tWaiting for training process finishes");
            // The returned iteration will be in progress, and can be queried periodically to see when it has completed
            while (iteration.Status == "Training")
            {
                Console.WriteLine($"\t...");

                Thread.Sleep(1000);

                // Re-query the iteration to get it's updated status
                iteration = trainingApi.GetIteration(project.Id, iteration.Id);
            }

            Console.WriteLine($"\tUpdating default iteration");
            // The iteration is now trained. Make it the default project endpoint
            iteration.IsDefault = true;
            trainingApi.UpdateIteration(project.Id, iteration.Id, iteration);



        }

        private static void CreateTheModel(string trainingSetPath, Project project)
        {
            var trainingKey = ConfigurationManager.AppSettings["CustomVision_TrainingKey"];
            var trainingApi = new TrainingApi() { ApiKey = trainingKey };

            var trainingModel = new List<Model>();

            var trainingSet = Directory.GetDirectories(trainingSetPath);
            foreach (var subdirectory in trainingSet)
            {
                var dir = new DirectoryInfo(subdirectory);
                var name = dir.Name;

                Console.WriteLine($"\tAdding Tag - {name}");
                var tag = trainingApi.CreateTag(project.Id, name);

                var images = Directory.GetFiles($"{subdirectory}").Select(f =>
                {
                    trainingModel.Add(new Model() { Label = name, Path = f });
                    return new MemoryStream(File.ReadAllBytes(f));
                }).ToList();

                foreach (var image in images)
                {
                    Console.WriteLine($"\tUploading image with tag: {tag.Name}");
                    trainingApi.CreateImagesFromData(project.Id, image, new List<string>() { tag.Id.ToString() });
                }
            }

            using (TextWriter writer = new StreamWriter($"{trainingSetPath}\\trainingModel.csv"))
            {
                var csv = new CsvWriter(writer);

                csv.WriteRecords(trainingModel);
            }
        }

        private static Project CreateProject(string projectName)
        {
            var trainingKey = ConfigurationManager.AppSettings["CustomVision_TrainingKey"];
            var trainingApi = new TrainingApi() { ApiKey = trainingKey };

            // Create a new project
            Console.WriteLine("\tCreating new project:");
            var project = trainingApi.CreateProject(projectName);
            return project;
        }


    }
}
