﻿using aibotPro.Interface;
using aibotPro.Models;
using AlibabaCloud.OpenApiClient.Models;
using AlibabaCloud.SDK.Captcha20230305;
using AlibabaCloud.SDK.Captcha20230305.Models;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OfficeOpenXml;
using Spire.Presentation;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Web;
using TiktokenSharp;

namespace aibotPro.Service
{
    public class SystemService : ISystemService
    {
        //依赖注入AIbotProContext
        private readonly AIBotProContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IRedisService _redis;
        public SystemService(AIBotProContext context, IHttpContextAccessor httpContextAccessor, IRedisService redis)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _redis = redis;
        }
        public bool SendEmail(string toemail, string title, string content)
        {
            SmtpClient client = new SmtpClient("smtp.qq.com", 587);
            client.EnableSsl = true;
            client.UseDefaultCredentials = false;
            //获取系统配置
            List<SystemCfg> systemConfig = GetSystemCfgs();
            //var systemConfig = _httpContextAccessor.HttpContext?.Items["SystemConfig"] as List<SystemCfg>;
            string fromEmail = string.Empty;
            string mailPwd = string.Empty;
            if (systemConfig != null)
            {
                fromEmail = systemConfig.Find(x => x.CfgKey == "Mail").CfgValue;
                mailPwd = systemConfig.Find(x => x.CfgKey == "MailPwd").CfgValue;
            }
            else
            {
                WriteLog("系统配置表为空", Dtos.LogLevel.Error, "system");
                return false;
            }
            client.Credentials = new NetworkCredential(fromEmail, mailPwd);
            // 创建电子邮件
            MailMessage mailMessage = new MailMessage(fromEmail, toemail, title, content);
            mailMessage.IsBodyHtml = true;
            try
            {
                // 发送邮件
                client.Send(mailMessage);
                return true;

            }
            catch (Exception ex)
            {
                WriteLog(ex.Message, Dtos.LogLevel.Error, "system");
                return false;
            }
        }
        public async Task WriteLog(string log, string logLevel, string CreateAccount)
        {
            SystemLog systemLog = new SystemLog();
            systemLog.LogTxt = log;
            systemLog.CreateTime = DateTime.Now;
            systemLog.CreateAccount = CreateAccount;
            systemLog.LogLevel = logLevel;
            await _context.SystemLogs.AddAsync(systemLog);
            await _context.SaveChangesAsync();
        }
        public void WriteLogUnAsync(string log, string logLevel, string CreateAccount)
        {
            SystemLog systemLog = new SystemLog();
            systemLog.LogTxt = log;
            systemLog.CreateTime = DateTime.Now;
            systemLog.CreateAccount = CreateAccount;
            systemLog.LogLevel = logLevel;
            _context.SystemLogs.Add(systemLog);
            _context.SaveChanges();
        }
        public string GenerateCode(int length)
        {
            // 包含数字和小写字母的字符集
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            StringBuilder code = new StringBuilder(length);
            Random random = new Random();

            for (int i = 0; i < length; i++)
            {
                int index = random.Next(chars.Length);
                code.Append(chars[index]);
            }

            return code.ToString();
        }
        public string ConvertToMD5(string str, int length = 16, bool lower = false)
        {
            //MD5加密
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] result = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(str));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < result.Length; i++)
            {
                sb.Append(result[i].ToString("x2"));
            }
            if (lower)
                return sb.ToString().ToLower().Substring(0, length);
            else
                return sb.ToString().Substring(0, length);
        }
        public bool SaveIP(string ip, string address)
        {
            //查询IP是否存在，今天是否已记录
            var iplook_this = _context.IPlooks
                .Where(x => x.IPv4 == ip && (x.LookTime == null || x.LookTime.Value.Date == DateTime.Now.Date))
                .FirstOrDefault();
            //如果存在，不处理，返回true
            if (iplook_this != null)
            {
                return true;
            }
            IPlook iplook = new IPlook()
            {
                IPv4 = ip,
                Address = address,
                LookTime = DateTime.Now
            };
            return _context.IPlooks.Add(iplook) != null;
        }
        public List<AImodel> GetAImodel()
        {
            var aiModel = _redis.GetAsync("AImodel").Result;
            List<AImodel> aiModel_lst = new List<AImodel>();
            //如果Redis中没有AI模型信息，则从数据库加载AI模型信息
            if (string.IsNullOrEmpty(aiModel))
            {
                // 从数据库加载AI模型信息
                aiModel_lst = _context.AImodels.ToList();
                //根据Seq排序
                aiModel_lst.Sort((x, y) => x.Seq.GetValueOrDefault().CompareTo(y.Seq));
                // 将配置信息存入Redis以便后续使用
                _redis.SetAsync("AImodel", JsonConvert.SerializeObject(aiModel_lst));
            }
            else
            {
                // 将配置信息从Redis中取出并反序列化
                aiModel_lst = JsonConvert.DeserializeObject<List<AImodel>>(aiModel);
                //根据Seq排序
                aiModel_lst.Sort((x, y) => x.Seq.GetValueOrDefault().CompareTo(y.Seq));
            }
            return aiModel_lst;
        }
        public List<WorkShopAIModel> GetWorkShopAImodel()
        {
            var aiModel = _redis.GetAsync("WorkShopAImodel").Result;
            List<WorkShopAIModel> aiModel_lst = new List<WorkShopAIModel>();
            //如果Redis中没有AI模型信息，则从数据库加载AI模型信息
            if (string.IsNullOrEmpty(aiModel))
            {
                // 从数据库加载AI模型信息
                aiModel_lst = _context.WorkShopAIModels.ToList();
                // 将配置信息存入Redis以便后续使用
                _redis.SetAsync("WorkShopAImodel", JsonConvert.SerializeObject(aiModel_lst));
            }
            else
            {
                // 将配置信息从Redis中取出并反序列化
                aiModel_lst = JsonConvert.DeserializeObject<List<WorkShopAIModel>>(aiModel);
            }
            return aiModel_lst;
        }
        public string EncodeBase64(string source)
        {
            //非空判断
            if (string.IsNullOrEmpty(source))
                return string.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(source);
            return Convert.ToBase64String(bytes);
        }
        public string DecodeBase64(string result)
        {
            byte[] outputb = Convert.FromBase64String(result);
            return Encoding.UTF8.GetString(outputb);
        }
        public string SaveFiles(string path, IFormFile file, string Account = "")
        {
            Account = string.IsNullOrEmpty(Account) ? "system" : Account;
            //如果文件夹不存在则创建
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            //把路径融合进文件名以-分隔
            string fileName = Guid.NewGuid().ToString().Replace("-", "");
            string fileExtension = System.IO.Path.GetExtension(file.FileName);
            string savePath = System.IO.Path.Combine(path, fileName + fileExtension);
            using (var stream = new FileStream(savePath, FileMode.Create))
            {
                file.CopyTo(stream);
            }
            //写入日志
            WriteLog($"文件{file.FileName}上传成功", Dtos.LogLevel.Info, Account);
            //返回文件相对路径
            return savePath;
        }
        public string ImgConvertToBase64(string imagePath)
        {
            byte[] imageBytes = File.ReadAllBytes(imagePath);
            string base64String = Convert.ToBase64String(imageBytes);
            return base64String;
        }
        public int TokenMath(string str, double divisor)
        {
            int result = 0;
            TikToken tikToken = TikToken.GetEncoding("cl100k_base");
            result = (int)Math.Floor(tikToken.Encode(str).Count * divisor);
            return result;
        }
        public List<SystemCfg> GetSystemCfgs()
        {
            List<SystemCfg> systemConfig = new List<SystemCfg>();
            var systemConfigStr = _redis.GetAsync("SystemConfig").Result;
            if (!string.IsNullOrEmpty(systemConfigStr))
                systemConfig = JsonConvert.DeserializeObject<List<SystemCfg>>(systemConfigStr);
            else
            {
                //从数据库加载系统配置信息
                systemConfig = _context.SystemCfgs.AsNoTracking().ToList();
            }
            return systemConfig;
        }

        public async Task<string> UploadFileChunkAsync(IFormFile file, int chunkNumber, string fileName, string filePathhead)
        {
            var folderName = System.IO.Path.Combine(filePathhead, DateTime.Now.ToString("yyyyMMdd"));
            //如果文件夹不存在则创建
            if (!Directory.Exists(folderName))
                Directory.CreateDirectory(folderName);
            var filePath = System.IO.Path.Combine(folderName, $"{chunkNumber}.tmp");
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return filePath;
        }

        public async Task<string> MergeFileAsync(string fileName, int totalChunks, string account, string filePathhead)
        {
            var folderName = System.IO.Path.Combine(filePathhead, DateTime.Now.ToString("yyyyMMdd"));
            //如果文件夹不存在则创建
            if (!Directory.Exists(folderName))
                Directory.CreateDirectory(folderName);
            var finalPath = System.IO.Path.Combine(folderName, fileName);

            using (var fs = new FileStream(finalPath, FileMode.Create))
            {
                for (int i = 1; i <= totalChunks; i++)
                {
                    var tempFilePath = System.IO.Path.Combine(folderName, $"{i}.tmp");
                    byte[] chunkBytes = await File.ReadAllBytesAsync(tempFilePath);
                    await fs.WriteAsync(chunkBytes, 0, chunkBytes.Length);
                    File.Delete(tempFilePath); // Optionally delete the chunk
                }
            }
            //记录日志
            await WriteLog($"文件{fileName}合并成功", Dtos.LogLevel.Info, account);
            return finalPath;  // Or return a relative URL/path as per your requirement
        }
        public async Task<bool> AlibabaCaptchaAsync(string captchaVerifyParam)
        {
            //获取系统配置
            var systemConfig = GetSystemCfgs();
            //获取AK,SK
            var ak = systemConfig.Find(x => x.CfgKey == "Alibaba_Captcha_AK").CfgValue;
            var sk = systemConfig.Find(x => x.CfgKey == "Alibaba_Captcha_SK").CfgValue;
            var endpoint = systemConfig.Find(x => x.CfgKey == "Alibaba_Captcha_Endpoint").CfgValue;
            Config config = new Config
            {
                // 您的AccessKey ID
                AccessKeyId = ak,
                // 您的AccessKey Secret
                AccessKeySecret = sk,
                Endpoint = endpoint
            };
            Client client = new Client(config);
            VerifyCaptchaRequest request = new VerifyCaptchaRequest();
            request.CaptchaVerifyParam = captchaVerifyParam;
            VerifyCaptchaResponse response = client.VerifyCaptcha(request);
            if (response.StatusCode == 200)
                return bool.Parse(response.Body.Result.VerifyResult.ToString());
            else
                return false;
        }
        public bool DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            //文件不存在就是删除
            return true;
        }
        public async Task<string> GetFileText(string path)
        {
            //判断文件类型
            string fileType = System.IO.Path.GetExtension(path);
            //如果是txt文件
            if (fileType == ".txt")
            {
                return await File.ReadAllTextAsync(path);
            }
            //如果是pdf文件
            else if (fileType == ".pdf")
            {
                using (PdfReader reader = new PdfReader(path))
                {
                    StringBuilder text = new StringBuilder();
                    for (int i = 1; i <= reader.NumberOfPages; i++)
                    {
                        text.Append(PdfTextExtractor.GetTextFromPage(reader, i));
                    }
                    return text.ToString();
                }
            }
            // 如果是Excel文件
            else if (fileType == ".xlsx" || fileType == ".xls")
            {
                ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                using (var stream = new FileStream(path, FileMode.Open))
                {
                    using (var package = new ExcelPackage(stream))
                    {
                        StringBuilder excelText = new StringBuilder();
                        var worksheet = package.Workbook.Worksheets[0];
                        int rowCount = worksheet.Dimension.Rows;
                        int colCount = worksheet.Dimension.Columns;

                        for (int row = 1; row <= rowCount; row++)
                        {
                            for (int col = 1; col <= colCount; col++)
                            {
                                var cellValue = worksheet.Cells[row, col].Value?.ToString();
                                excelText.Append("|");
                                excelText.Append(cellValue);
                            }
                            excelText.AppendLine("|");
                        }

                        return excelText.ToString();
                    }
                }
            }
            //如果是PPT
            else if (fileType == ".pptx" || fileType == ".ppt")
            {
                // 确保传入的文件不是null
                using (var memoryStream = new MemoryStream())
                {
                    // 初始化Presentation类的实例
                    using (var presentation = new Presentation())
                    {
                        // 从内存流加载PowerPoint文档
                        presentation.LoadFromFile(path);
                        StringBuilder sb = new StringBuilder();

                        // 遍历文档中的每张幻灯片
                        foreach (ISlide slide in presentation.Slides)
                        {
                            // 遍历每张幻灯片中的每个形状
                            foreach (Spire.Presentation.IShape shape in slide.Shapes)
                            {
                                // 检查形状是否为IAutoShape类型
                                if (shape is IAutoShape autoShape)
                                {
                                    // 以每种形状遍历所有段落
                                    foreach (TextParagraph tp in autoShape.TextFrame.Paragraphs)
                                    {
                                        // 提取文本并保存到StringBuilder实例中
                                        sb.AppendLine(tp.Text);
                                    }
                                }
                            }
                        }
                        // 返回提取的文本
                        return sb.ToString();
                    }
                }
            }
            //如果是word
            else if (fileType == ".docx" || fileType == ".doc")
            {
                // 确保文件不为空
                string extractedText = string.Empty;
                try
                {
                    using (Stream stream = new FileStream(path, FileMode.Open))
                    {
                        // 创建Document对象
                        Spire.Doc.Document document = new Spire.Doc.Document();

                        // 加载Word文档
                        document.LoadFromStream(stream, Spire.Doc.FileFormat.Auto);

                        // 使用StringBuilder来保存提取的文本
                        StringBuilder sb = new StringBuilder();

                        // 遍历文档中的段落
                        foreach (Spire.Doc.Section section in document.Sections)
                        {
                            foreach (Spire.Doc.Documents.Paragraph paragraph in section.Paragraphs)
                            {
                                // 提取段落中的文本
                                sb.AppendLine(paragraph.Text);
                            }
                        }

                        // 将提取的文本转换为字符串
                        extractedText = sb.ToString();
                    }
                }
                catch (Exception ex)
                {
                    // 处理异常
                    await WriteLog(ex.Message, Dtos.LogLevel.Error, "system");
                }
                return extractedText;
            }
            else
            {
                return "暂不支持该文件类型";
            }
        }
        public string UrlEncode(string text)
        {
            return HttpUtility.UrlEncode(text);
        }

        public string UrlDecode(string encodedText)
        {
            return HttpUtility.UrlDecode(encodedText);
        }
    }
}
