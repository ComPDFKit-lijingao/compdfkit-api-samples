﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PDFConversion
{
    class PDFCompare
    {
        // PDF 文件路径
        private static readonly string pdfPath1 = "input_files/compare1.pdf";
        private static readonly string pdfPath2 = "input_files/compare2.pdf";
        // 项目 public Key：您可以在 ComPDFKit API 控制台的 API Key 板块获取
        private static readonly string publicKey = "public_key_******";
        // 项目 secret Key：您可以在 ComPDFKit API 控制台的 API Key 板块获取
        private static readonly string secretKey = "secret_key_******";

        private static readonly string AUTH_URL = "https://api-server.compdf.com/server/v1/oauth/token";
        private static readonly string CREATE_TASK_URL = "https://api-server.compdf.com/server/v1/task";
        private static readonly string UPLOAD_FILE_URL = "https://api-server.compdf.com/server/v1/file/upload";
        private static readonly string EXECUTE_TASK_URL = "https://api-server.compdf.com/server/v1/execute/start";
        private static readonly string GET_TASK_INFO_URL = "https://api-server.compdf.com/server/v1/task/taskInfo";

        static async Task Main(string[] args)
        {
            // 1. Authentication
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
                var tokenParam = new Dictionary<string, string>
                {
                    ["publicKey"] = publicKey,
                    ["secretKey"] = secretKey
                };
                
                var responseEntity = client.PostAsync(AUTH_URL, new StringContent(JsonConvert.SerializeObject(tokenParam), Encoding.UTF8, "application/json")).Result;

                var authResponseString = responseEntity.Content.ReadAsStringAsync().Result;
                var result = JsonConvert.DeserializeObject(authResponseString) as Newtonsoft.Json.Linq.JObject;
                if(result == null)
                {
                    Console.WriteLine("Failed to get ComPDFKit Token");
                    return;
                }
                string bearerToken = string.Empty;
                if (result.TryGetValue("data", out var data1))
                {
                    var dataObject1 = data1 as Newtonsoft.Json.Linq.JObject;
                    if (dataObject1 != null && dataObject1.TryGetValue("accessToken", out var accessToken))
                    {
                        bearerToken = accessToken.ToString();
                    }
                    else
                    {
                        Console.WriteLine("Failed to get accessToken from data.");
                    }
                }
                else
                {
                    Console.WriteLine("Failed to get data from result.");
                }

                Console.WriteLine($"bearerToken: {bearerToken}");

                // 2. Create Task
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
                client.DefaultRequestHeaders.Connection.Add("keep-alive");
                
                var createTaskResponse = client.GetAsync($"{CREATE_TASK_URL}/pdf/contentCompare?language=2").Result;
                var createTaskResponseString = createTaskResponse.Content.ReadAsStringAsync().Result;
                var createTaskJsonObject = JsonConvert.DeserializeObject(createTaskResponseString) as Newtonsoft.Json.Linq.JObject;
                if(createTaskJsonObject == null)
                {
                    Console.WriteLine("Failed to create task");
                    return;
                }

                string taskId;
                if (createTaskJsonObject.TryGetValue("data", out var dataObject))
                {
                    var data = dataObject as Newtonsoft.Json.Linq.JObject;
                    if (data != null && data.TryGetValue("taskId", out var taskIdObject))
                    {
                        taskId = taskIdObject.ToString();
                    }
                    else
                    {
                        Console.WriteLine("Failed to get taskId from data.");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Failed to get data from createTaskJsonObject.");
                    return;
                }

                // 3. Upload File1
                var fileContent1 = new StreamContent(File.OpenRead(pdfPath1));
                var formData1 = new MultipartFormDataContent();
                formData1.Add(fileContent1, "file", Path.GetFileName(pdfPath1));
                formData1.Add(new StringContent(taskId), "taskId");
                formData1.Add(new StringContent("{ \"imgCompare\":\"1\", \"isSaveTwo\":\"0\", \"textCompare\":\"1\", \"replaceColor\":\"#FF0000\", \"insertColor\":\"#00FF00\", \"deleteColor\":\"#0000FF\"}"), "parameter");
                formData1.Add(new StringContent("2"), "language");

                var uploadFileResponse1 = client.PostAsync(UPLOAD_FILE_URL, formData1).Result;
                if (!uploadFileResponse1.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Upload File1 Failed: {uploadFileResponse1.StatusCode}, {uploadFileResponse1.ReasonPhrase}");
                    return;
                }
                var uploadFileResponseString1 = uploadFileResponse1.Content.ReadAsStringAsync().Result;
                Console.WriteLine($"Upload File1 Result: {uploadFileResponseString1}");

                // Upload File2
                var fileContent2 = new StreamContent(File.OpenRead(pdfPath2));
                var formData2 = new MultipartFormDataContent();
                formData2.Add(fileContent2, "file", Path.GetFileName(pdfPath2));
                formData2.Add(new StringContent(taskId), "taskId");
                formData2.Add(new StringContent("{ \"imgCompare\":\"1\", \"isSaveTwo\":\"0\", \"textCompare\":\"1\", \"replaceColor\":\"#FF0000\", \"insertColor\":\"#00FF00\", \"deleteColor\":\"#0000FF\"}"), "parameter");
                formData2.Add(new StringContent("2"), "language");

                var uploadFileResponse2 = client.PostAsync(UPLOAD_FILE_URL, formData2).Result;
                if (!uploadFileResponse2.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Upload File2 Failed: {uploadFileResponse2.StatusCode}, {uploadFileResponse2.ReasonPhrase}");
                    return;
                }
                var uploadFileResponseString2 = uploadFileResponse2.Content.ReadAsStringAsync().Result;
                Console.WriteLine($"Upload File2 Result: {uploadFileResponseString2}");

                // 4. Execute Task
                var executeTaskResponse = client.GetAsync($"{EXECUTE_TASK_URL}?language=2&taskId={taskId}").Result;
                var executeTaskResponseString = executeTaskResponse.Content.ReadAsStringAsync().Result;
                Console.WriteLine($"Execute Task Result: {executeTaskResponseString}");

                // 5. Get Task Information
                bool flag = true;
                while (flag)
                {
                    await Task.Delay(1000);
                    var getTaskInfoResponse = client.GetAsync($"{GET_TASK_INFO_URL}?taskId={taskId}").Result;
                    var getTaskInfoResponseString = getTaskInfoResponse.Content.ReadAsStringAsync().Result;
                    dynamic getTaskInfoJsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject(getTaskInfoResponseString);
                    string taskStatus = getTaskInfoJsonObject.data.taskStatus;
                    if (taskStatus == "TaskFinish")
                    {
                        Console.WriteLine($"Task Information: {getTaskInfoResponseString}");
                        flag = false;
                    }
                }
            }
        }
    }
}
