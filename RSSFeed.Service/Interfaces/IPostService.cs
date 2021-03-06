﻿using RSSFeed.Data.Entities;
using RSSFeed.Service.Enums;
using RSSFeed.Service.Models;
using RSSFeed.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RSSFeed.Service.Interfaces
{
    public interface IPostService : IBaseQueryService<Post, PostModel, PostSortType>
    {
        
        IEnumerable<PostModel> GetPosts(Guid channelId);
        IEnumerable<PostModel> GetPosts();
        IEnumerable<PostModel> GetFinancialPosts();
        Task<PostModel> GetPostByIdAsync(Guid id);
        void AddPost(PostModel postModel);
        Task<IDictionary<PostModel, CategoryModel>> FeedItems(ChannelModel channel);
        void PostSeen(Guid postId);
        void SetPostIsNotNewYet(Guid postId);
    }
}
