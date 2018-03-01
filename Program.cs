using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CustomVisionEnd2End.Models;
using CustomVisionEnd2End.Services;
using Microsoft.Cognitive.CustomVision.Prediction;
using Microsoft.Cognitive.CustomVision.Training;
using Microsoft.Cognitive.CustomVision.Training.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

using Emgu.CV;
using Emgu.CV.Structure;

namespace CustomVisionEnd2End
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var stopWatch = new Stopwatch();

                stopWatch.Start();

                MainAsync(args).GetAwaiter().GetResult();

                stopWatch.Stop();
                // Get the elapsed time as a TimeSpan value.
                var ts = stopWatch.Elapsed;

                // Format and display the TimeSpan value.
                var elapsedTime = $"\t{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds / 10:00}";
                Console.WriteLine("\tRunTime: " + elapsedTime);



            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }
            Console.WriteLine($"\tPress any key to exit");
            Console.ReadKey();

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

            var projectName = tags.Count == 2 ? $"{tags[0]} {tags[1]} classifier {DateTime.Now:yyyyMMddHHmm}" : DateTime.Now.ToString("yyyyMMddHHmm");
            Console.WriteLine($"\tCreating a project in customvision.ai - project name: {projectName}");
            
            var project = CreateProject(projectName);

            //download images - 
            var imagesresouce = $"{basepath}\\{projectName}\\data\\";
            var trainingSetPath = $"{imagesresouce}TrainingSet";
            var testSetPath = $"{imagesresouce}TestSet";
            Console.WriteLine($"\tBing search & downloading images - split them in TrainingSet & TestSet for each tag");
            Console.WriteLine($"\tTrainingSetPath: {trainingSetPath}");
            Console.WriteLine($"\tTestSetPath: {testSetPath}");
            System.IO.Directory.CreateDirectory(trainingSetPath);
            System.IO.Directory.CreateDirectory(testSetPath);


            //int minTrainingPhotosCount = 60; //60;
            //int minTestPhotosCount = 20; // 20;

            int sizeOfImageSet = 10;
            try
            {
                sizeOfImageSet = Convert.ToInt16(ConfigurationManager.AppSettings["sizeOfImageSet"]); //recommend 100
            }
            catch { }

            foreach (var tag in tags)
            {
                {
                    using (var bingImageSearchService = new BingImageSearchService())
                    {
                        Console.WriteLine($"\tStarting the Process for : {tag}");

                        var bingresult = await bingImageSearchService.ImageSearch(tag, sizeOfImageSet);
                        if (bingresult.value == null) return;
                        //
                        using (var writer = new StreamWriter($"{imagesresouce}\\{tag}_resource.csv"))
                        {
                            using (var csvWriter = new CsvWriter(writer))
                            {
                                csvWriter.WriteRecords(bingresult.value);
                            }
                        }

                        //
                        //training 
                        Console.WriteLine($"\tDownloading the training and test set");
                        var trainingphotos = DownloadImages(projectName, tag, bingresult.value.ToList(), sizeOfImageSet);
                        //test
                        //Console.WriteLine($"\tDownloading the test set");
                        //var testphotos = DownloadImages($"{testSetPath}\\{tag}", bingresult.value.Skip(trainingphotos).ToList(), minTestPhotosCount, false);

                        //if (trainingphotos < minTrainingPhotosCount || testphotos < minTestPhotosCount)
                        //{
                        //    throw new Exception($"Bing couldn't find required images.you need at least 2 tags and 5 images for each tag to start");
                        //}
                    }
                }

            }

            

            CreateTheModel(trainingSetPath, project);

            // Now there are images with tags start training the project
            TrainTheModel(project);

            Console.WriteLine($"\tTesting the Model");

            TestingTheModel(testSetPath, project);

        }

        private static List<string> ReadTags(string tsgfilepath)
        {
            try
            {
                var result = new List<string>();

                using (TextReader fileReader = File.OpenText(tsgfilepath))
                {
                    var csv = new CsvReader(fileReader);
                    csv.Configuration.HasHeaderRecord = false;
                    while (csv.Read())
                    {
                        string value;
                        for (var i = 0; csv.TryGetField(i, out value); i++)
                        {
                            if (!string.IsNullOrEmpty(value))
                            {
                                result.Add(value);
                            }
                        }
                    }
                }
                return result;


            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        /// <summary>
        /// Returns the contents of the specified file as a byte array.
        /// </summary>
        /// <param name="imageFilePath">The image file to read.</param>
        /// <returns>The byte array of the image data.</returns>
        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);
            return binaryReader.ReadBytes((int)fileStream.Length);
        }

        /// <summary>
        /// Formats the given JSON string by adding line breaks and indents.
        /// </summary>
        /// <param name="json">The raw JSON string to format.</param>
        /// <returns>The formatted JSON string.</returns>
        static string JsonPrettyPrint(string json)
        {
            if (string.IsNullOrEmpty(json))
                return string.Empty;

            json = json.Replace(Environment.NewLine, "").Replace("\t", "");

            string INDENT_STRING = "    ";
            var indent = 0;
            var quoted = false;
            var sb = new StringBuilder();
            for (var i = 0; i < json.Length; i++)
            {
                var ch = json[i];
                switch (ch)
                {
                    case '{':
                    case '[':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            Enumerable.Range(0, ++indent).ForEach(item => sb.Append(INDENT_STRING));
                        }
                        break;
                    case '}':
                    case ']':
                        if (!quoted)
                        {
                            sb.AppendLine();
                            Enumerable.Range(0, --indent).ForEach(item => sb.Append(INDENT_STRING));
                        }
                        sb.Append(ch);
                        break;
                    case '"':
                        sb.Append(ch);
                        bool escaped = false;
                        var index = i;
                        while (index > 0 && json[--index] == '\\')
                            escaped = !escaped;
                        if (!escaped)
                            quoted = !quoted;
                        break;
                    case ',':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            Enumerable.Range(0, indent).ForEach(item => sb.Append(INDENT_STRING));
                        }
                        break;
                    case ':':
                        sb.Append(ch);
                        if (!quoted)
                            sb.Append(" ");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }


        /// <summary>
        /// Gets a thumbnail image from the specified image file by using the Computer Vision REST API.
        /// </summary>
        /// <param name="imageFilePath">The image file to use to create the thumbnail image.</param>
        static async void SmartResizeImage(string imageurl, string pathString, string filename, bool isTraining)
        {
            string imageFilePath = pathString + "\\" + filename;

            HttpClient client = new HttpClient();

            string subscriptionKey = ConfigurationManager.AppSettings["ComputerVision_Key"];
            // e.g. https://westus2.api.cognitive.microsoft.com/vision/v1.0/generateThumbnail
            string uriBase = ConfigurationManager.AppSettings["ComputerVision_Url"];

            // Request headers.
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

            // Request parameters.
            // setting the size for the image is dependant on the model for transfer learning and you can experiment with this to try and get the best results for your dataset
            // '299', '224', '192', '160', or '128' for the input image size, with smaller sizes giving faster speeds during training
            // I recommend if you plan to export the model generated for tensorflow transfer learning with ResNet V2 models use image size of 299
            string requestParameters = "width=299&height=299&smartCropping=true";

            // Assemble the URI for the REST API Call.
            string uri = uriBase + "?" + requestParameters;

            HttpResponseMessage response;

            // Request body. Posts a locally stored JPEG image.
            //byte[] byteData = GetImageAsByteArray(imageFilePath);

            var jsonBody = "{'url': '" + imageurl + "'}";
            byte[] byteData = Encoding.UTF8.GetBytes(jsonBody);

            using (ByteArrayContent content = new ByteArrayContent(byteData))
            {
                // This example uses content type "application/octet-stream".
                // The other content types you can use are "application/json" and "multipart/form-data".
                //content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                // Execute the REST API call.
                response = await client.PostAsync(uri, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsByteArrayAsync();
                    System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(new MemoryStream(responseContent));
                    AugmentImage(bitmap, pathString, filename, isTraining);

                    /*
                    // write the image file
                    var responseContent = await response.Content.ReadAsByteArrayAsync();
                    using (BinaryWriter binaryWrite = new BinaryWriter(new FileStream(imageFilePath, FileMode.Create, FileAccess.Write)))
                    {
                        binaryWrite.Write(responseContent);
                    }
                    */

                }
                else
                {
                    // Display the JSON error data.
                    Console.WriteLine("\nError: Thumbnail not created\n");
                    Console.WriteLine(JsonPrettyPrint(await response.Content.ReadAsStringAsync()));
                }
            }

        }

        /*
        Although Transfer learning requires a smaller dataset it still needs conisitency and image preperation steps to deliver good results.
        
        Data preparation is required when working with neural network and deep learning models.
        Increasingly data augmentation is also required on more complex object recognition tasks.

        The more data an ML algorithm has access to, the more effective it can be. 
        Even when the data is of lower quality, algorithms can actually perform better, as long as useful data can be extracted by the model from the original data set.

        http://cs231n.stanford.edu/reports/2017/pdfs/300.pdf
        https://arxiv.org/pdf/1609.08764.pdf
        https://towardsdatascience.com/image-augmentation-for-deep-learning-histogram-equalization-a71387f609b2
        http://imgaug.readthedocs.io/en/latest/source/examples_basics.html

        */

        private static int RandomNumberOdd(int min, int max)
        {
            Random random = new Random();
            int ans = random.Next(min, max);
            if (ans % 2 == 1) return ans;
            else
            {
                if (ans + 1 <= max)
                    return ans + 1;
                else if (ans - 1 >= min)
                    return ans - 1;
                else return 0;
            }
        }

        private static void AugmentImage(System.Drawing.Bitmap img, string pathString, string filename, bool isTraining)
        {
            string imageFilePath = pathString + "\\" + filename;
            // flip from RGB to BGR
            Image<Bgr, byte> ogImg = new Image<Bgr, byte>(img);

            // perform Intensity Image Equalization
            Image<Ycc, byte> ycrcb = ogImg.Convert<Ycc, byte>();
            ycrcb._EqualizeHist();
            ogImg = ycrcb.Convert<Bgr, byte>(); //replace original image with equalized image

            if (isTraining) { 
                // for training images perform additional image augmentation steps

                //Small gaussian blur for about half of the training images with a random odd kernelSize 1,3 or 5
                if (Convert.ToBoolean(new Random().Next(0, 2)))
                {
                    ogImg._SmoothGaussian(RandomNumberOdd(1, 5));
                }

                // - rotate by -45 to +45 degrees five times
                for (var i = 0; i < 5; i++)
                {
                    WriteImageFile(ogImg.Rotate(new Random().Next(-45, 45), new Bgr(0, 0, 0)), imageFilePath, "_rotated"+i);
                }

                // flip the image horizontally
                WriteImageFile(ogImg.Flip(Emgu.CV.CvEnum.FlipType.Horizontal), imageFilePath, "_flipped");

            }
            WriteImageFile(ogImg, imageFilePath);

        }


        private static void WriteImageFile(Image<Bgr, byte> img, string imageFilePath, string typeStr="")
        {
            using (BinaryWriter binaryWrite = new BinaryWriter(new FileStream(imageFilePath.Replace(".jpg", "") + typeStr+".jpg", FileMode.Create, FileAccess.Write)))
            {
                binaryWrite.Write(img.ToJpegData(95));
            }
        }


        private static int DownloadImages(string projectName, string tag, List<Value> items, int numberofimagestodownload)
        {
            var counter = 1;

            var basepath = Directory.GetCurrentDirectory();
            var imagesresouce = $"{basepath}\\{projectName}\\data\\";
            var trainingSetPath = $"{imagesresouce}TrainingSet\\{tag}";
            var testSetPath = $"{imagesresouce}TestSet\\{tag}";

            Directory.CreateDirectory(trainingSetPath);
            Directory.CreateDirectory(testSetPath);

            string pathString;
            bool isTraining;
            
            foreach (var item in items)
            {
                if ((new Random().Next(1, 11)) <= 7)
                {
                    // randomly 70% of the time is training 30% of the time is test
                    isTraining = true;
                    pathString = trainingSetPath;
                }
                else
                {
                    isTraining = false;
                    pathString = testSetPath;
                }
                
                if (counter%5==0) { 
                    //API is rate limited wait 5 seconds after every 5 calls
                    System.Threading.Thread.Sleep(2000);
                }

                if (numberofimagestodownload > 0 && counter > numberofimagestodownload)
                {
                    return numberofimagestodownload;
                }
                try
                {
                    counter = counter + 1;
                    Console.WriteLine("\tDownloading the image");
                    var imageurl = item.contentUrl;
                    var webClient = new WebClient();
                    var uri = new Uri(imageurl);
                    var filename = uri.Segments.Last();

                    //webClient.DownloadFile(imageurl, pathString + "\\" + filename);

                    // rezise the image using smart crop service
                    SmartResizeImage(imageurl, pathString, filename, isTraining);
                    
                }
                catch (Exception e)
                {
                    //kill exception and carry on
                    Console.WriteLine("\tImage download error: " + e.Message);
                    counter = counter - 1;
                }
            }
            return counter;
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
                    try
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
                    catch (Exception e)
                    {
                        //kill exception and carry on
                        Console.WriteLine(e);
                    }


                }
            }
            try
            {
                using (TextWriter writer = new StreamWriter($"{testingSetPath}\\testModel.csv"))
                {
                    var csv = new CsvWriter(writer);

                    csv.WriteRecords(testModel);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }

            var array2D = GenerateConfusionMatrix(labels, predictionResult);

            //pretty print
            PrettyPrint(array2D);


            ExporttoCSV(testingSetPath, array2D);
        }

        private static string[,] GenerateConfusionMatrix(List<string> labels, Dictionary<string, int> predictionResult)
        {
            labels = labels.Distinct().ToList();

            // Two-dimensional array. [row,col]
            var array2D = new string[labels.Count + 1, labels.Count + 1];

            try
            {


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

            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }
            return array2D;
        }

        private static void ExporttoCSV(string testingSetPath, string[,] array2D)
        {
            try
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
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void PrettyPrint(string[,] array2D)
        {
            try
            {
                var rowLength = array2D.GetLength(1);
                var colLength = array2D.GetLength(0);

                Console.WriteLine("\t--------------------------------------------------------------");


                for (var i = 0; i < rowLength; i++)
                {
                    for (var j = 0; j < colLength; j++)
                    {
                        Console.Write($"\t{array2D[i, j],-30}");
                    }
                    Console.Write(Environment.NewLine + Environment.NewLine);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }
        }

        private static void TrainTheModel(Project project)
        {
            try
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
            catch (Exception e)
            {
                Console.WriteLine(e);

            }


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
                    try
                    {

                        Console.WriteLine($"\tUploading image with tag: {tag.Name}");
                        trainingApi.CreateImagesFromData(project.Id, image, new List<string>() { tag.Id.ToString() });
                    }
                    catch (Exception e)
                    {
                        //kill exception and carry on
                        Console.WriteLine(e);

                    }
                }
            }

            try
            {
                using (TextWriter writer = new StreamWriter($"{trainingSetPath}\\trainingModel.csv"))
                {
                    var csv = new CsvWriter(writer);

                    csv.WriteRecords(trainingModel);
                }


            }
            catch (Exception e)
            {
                Console.WriteLine(e);

            }

        }

        private static Project CreateProject(string projectName)
        {
            try
            {
                var trainingKey = ConfigurationManager.AppSettings["CustomVision_TrainingKey"];
                var trainingApi = new TrainingApi() { ApiKey = trainingKey };

                // Create a new project
                Console.WriteLine("\tCreating new project:");
                var project = trainingApi.CreateProject(projectName);
                return project;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }


    }

    static class Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> ie, Action<T> action)
        {
            foreach (var i in ie)
            {
                action(i);
            }
        }
    }
}
