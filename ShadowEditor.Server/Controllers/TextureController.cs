﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using ShadowEditor.Server.Base;
using ShadowEditor.Server.Helpers;
using ShadowEditor.Server.Texture;

namespace ShadowEditor.Server.Controllers
{
    /// <summary>
    /// 纹理控制器
    /// </summary>
    public class TextureController : ApiBase
    {
        /// <summary>
        /// 获取列表
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public JsonResult List()
        {
            var mongo = new MongoHelper();
            var docs = mongo.FindAll(Constant.TextureCollectionName);

            var list = new List<TextureModel>();

            foreach (var i in docs)
            {
                var info = new TextureModel
                {
                    ID = i["ID"].AsObjectId.ToString(),
                    Name = i["Name"].AsString,
                    TotalPinYin = i["TotalPinYin"].ToString(),
                    FirstPinYin = i["FirstPinYin"].ToString(),
                    Type = i["Type"].AsString,
                    Url = i["Url"].AsString,
                    CreateTime = i["CreateTime"].ToUniversalTime(),
                    UpdateTime = i["UpdateTime"].ToUniversalTime(),
                    Thumbnail = i["Thumbnail"].ToString()
                };
                list.Add(info);
            }

            list = list.OrderByDescending(o => o.UpdateTime).ToList();

            return Json(new
            {
                Code = 200,
                Msg = "获取成功！",
                Data = list
            });
        }

        /// <summary>
        /// 保存纹理
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public JsonResult Add()
        {
            var file = HttpContext.Current.Request.Files[0];
            var fileName = file.FileName;
            var fileSize = file.ContentLength;
            var fileType = file.ContentType;
            var fileExt = Path.GetExtension(fileName);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            if (fileExt == null || fileExt.ToLower() != ".jpg" && fileExt.ToLower() != ".jpeg" && fileExt.ToLower() != ".png")
            {
                return Json(new Result
                {
                    Code = 300,
                    Msg = "只允许上传jpg或png格式文件！"
                });
            }

            // 保存文件
            var now = DateTime.Now;

            var savePath = $"/Upload/Texture/{now.ToString("yyyyMMddHHmmss")}";
            var physicalPath = HttpContext.Current.Server.MapPath(savePath);

            if (!Directory.Exists(physicalPath))
            {
                Directory.CreateDirectory(physicalPath);
            }

            file.SaveAs($"{physicalPath}\\{fileName}");

            var pinyin = PinYinHelper.GetTotalPinYin(fileNameWithoutExt);

            // 保存到Mongo
            var mongo = new MongoHelper();

            var doc = new BsonDocument();
            doc["ID"] = ObjectId.GenerateNewId();
            doc["AddTime"] = BsonDateTime.Create(now);
            doc["FileName"] = fileName;
            doc["FileSize"] = fileSize;
            doc["FileType"] = fileType;
            doc["FirstPinYin"] = string.Join("", pinyin.FirstPinYin);
            doc["Name"] = fileNameWithoutExt;
            doc["SaveName"] = fileName;
            doc["SavePath"] = savePath;
            doc["Thumbnail"] = $"{savePath}/{fileName}";
            doc["TotalPinYin"] = string.Join("", pinyin.TotalPinYin);
            doc["Type"] = TextureType.unknown.ToString();
            doc["Url"] = $"{savePath}/{fileName}";
            doc["CreateTime"] = now;
            doc["UpdateTime"] = now;

            mongo.InsertOne(Constant.TextureCollectionName, doc);

            return Json(new Result
            {
                Code = 200,
                Msg = "上传成功！"
            });
        }

        /// <summary>
        /// 编辑纹理
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        public JsonResult Edit(EditTextureModel model)
        {
            var objectId = ObjectId.GenerateNewId();

            if (!string.IsNullOrEmpty(model.ID) && !ObjectId.TryParse(model.ID, out objectId))
            {
                return Json(new
                {
                    Code = 300,
                    Msg = "ID不合法。"
                });
            }

            if (string.IsNullOrEmpty(model.Name))
            {
                return Json(new
                {
                    Code = 300,
                    Msg = "名称不允许为空。"
                });
            }

            var mongo = new MongoHelper();

            var pinyin = PinYinHelper.GetTotalPinYin(model.Name);

            var filter = Builders<BsonDocument>.Filter.Eq("ID", objectId);
            var update1 = Builders<BsonDocument>.Update.Set("Name", model.Name);
            var update2 = Builders<BsonDocument>.Update.Set("TotalPinYin", pinyin.TotalPinYin);
            var update3 = Builders<BsonDocument>.Update.Set("FirstPinYin", pinyin.FirstPinYin);
            var update = Builders<BsonDocument>.Update.Combine(update1, update2, update3);
            mongo.UpdateOne(Constant.TextureCollectionName, filter, update);

            return Json(new
            {
                Code = 200,
                Msg = "保存成功！"
            });
        }

        /// <summary>
        /// 删除纹理
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        [HttpPost]
        public JsonResult Delete(string ID)
        {
            var mongo = new MongoHelper();

            var filter = Builders<BsonDocument>.Filter.Eq("ID", BsonObjectId.Create(ID));
            var doc = mongo.FindOne(Constant.TextureCollectionName, filter);

            if (doc == null)
            {
                return Json(new
                {
                    Code = 300,
                    Msg = "该纹理不存在！"
                });
            }

            // 删除纹理所在目录
            var path = doc["SavePath"].ToString();
            var physicalPath = HttpContext.Current.Server.MapPath(path);

            try
            {
                Directory.Delete(physicalPath, true);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    Code = 300,
                    Msg = ex.Message
                });
            }

            // 删除纹理信息
            mongo.DeleteOne(Constant.TextureCollectionName, filter);

            return Json(new
            {
                Code = 200,
                Msg = "删除纹理成功！"
            });
        }
    }
}
