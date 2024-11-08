﻿using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Manager_App.Models
{
    public class ManagerAccount
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("username")]
        public string Username { get; set; }

        [BsonElement("password")]
        public string Password { get; set; } // Сохраняем хэш пароля
    }
}