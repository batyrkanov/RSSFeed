﻿using Newtonsoft.Json;
using RSSFeed.Service.Models.Base;
using System;
using System.Collections.Generic;

namespace RSSFeed.Service.Models
{
    public class PostModel : BaseModel
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public string PostUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? ChannelId { get; set; }
        public ChannelModel Channel { get; set; }
        public bool IsSeen { get; set; }
        public bool IsNew { get; set; }
        public string CategoryName { get; set; }
        public string ImageUrl { get; set; }
    }
}
