﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantAPI2.Authorization;
using RestaurantAPI2.Entities;
using RestaurantAPI2.Exceptions;
using RestaurantAPI2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RestaurantAPI2.Services
{
    public interface IRestaurantService
    {
        RestaurantDto GetById(int id);
        PagedResult<RestaurantDto> GetAll(RestaurantQuery query);
        int Create(CreateRestaurantDto dto);
        void Delete(int id);
        void Update(int id, UpdateRestaurantDto dto);
    }


    //RestaurantService został utworzony, ponieważ jedynym zadaniem w akcjach w kontrolerze powinno być odebranie zapytania, ewentualnie jego walidacja 
    //oraz przesłanie takiego zapytania do jakiegoś serwisu i to własnie ten serwis powinien go obsłużyć
    public class RestaurantService : IRestaurantService
    {
        private readonly RestaurantDbContext _dbContext;
        private readonly IMapper _mapper;
        private readonly ILogger<RestaurantService> _logger;
        private readonly IAuthorizationService _authorizationService;
        private readonly IUserContextService _userContextService;

        public RestaurantService(RestaurantDbContext dbContext, IMapper mapper, ILogger<RestaurantService> logger
            , IAuthorizationService authorizationService, IUserContextService userContextService)
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _logger = logger;
            _authorizationService = authorizationService;
            _userContextService = userContextService;
        }

        public RestaurantDto GetById(int id)
        {
            var restaurant = _dbContext
              .Restaurants
              .Include(r => r.Address)
              .Include(r => r.Dishes)
              .FirstOrDefault(r => r.Id == id);

            if (restaurant is null)
                throw new NotFoundException("RestaurantNotFound");

            var result = _mapper.Map<RestaurantDto>(restaurant);
            return result;
        }

        public PagedResult<RestaurantDto> GetAll(RestaurantQuery query) // ""kfc" : "KFC"
        {
            var baseQuery = _dbContext
                 .Restaurants
                 .Include(r => r.Address)
                 .Include(r => r.Dishes)
                 .Where(r => query.SearchPhrase == null || (r.Name.ToLower().Contains(query.SearchPhrase.ToLower())
                            || r.Description.ToLower().Contains(query.SearchPhrase.ToLower())));

            if (!string.IsNullOrEmpty(query.SortBy))
            {
                var columnsSelector = new Dictionary<string, Expression<Func<Restaurant, object>>>
                    {
                        { nameof(Restaurant.Name), r => r.Name},
                        { nameof(Restaurant.Description), r => r.Description},
                        { nameof(Restaurant.Category), r => r.Category},
                    };

                var selectedColumn = columnsSelector[query.SortBy];


                    baseQuery = query.SortDirection == SortDirection.ASC
                        ? baseQuery.OrderBy(selectedColumn)
                        : baseQuery.OrderByDescending(selectedColumn);
            }

            var restuarants = baseQuery
                 .Skip(query.PageSize * (query.PageNumber - 1)) //pominięcie określonej ilości elementów
                 .Take(query.PageSize) //wzięcie określonej liczby elementów
                 .ToList();

            var totalitemsCount = baseQuery.Count();

            var restaurantsDtos = _mapper.Map<List<RestaurantDto>>(restuarants);

            var result = new PagedResult<RestaurantDto>(restaurantsDtos, totalitemsCount, query.PageSize, query.PageNumber);

            return result;
        }

        public int Create(CreateRestaurantDto dto)
        {
            var restaurant = _mapper.Map<Restaurant>(dto);
            restaurant.CreatedById = _userContextService.GetUserId;
            _dbContext.Restaurants.Add(restaurant);
            _dbContext.SaveChanges();

            return restaurant.Id;
        }

        public void Delete(int id)
        {
            _logger.LogError($"Restaurant with id: {id} DELETE action invoked");

            var restaurant = _dbContext
              .Restaurants             
              .FirstOrDefault(r => r.Id == id);

            if (restaurant is null)
                throw new NotFoundException("RestaurantNotFound");

            var authorizationResult = _authorizationService.AuthorizeAsync(_userContextService.User, restaurant,
            new ResourceOperationRequirement(ResourceOperation.Delete)).Result;

            if (!authorizationResult.Succeeded)
            {
                throw new ForbidException();
            }

            _dbContext.Restaurants.Remove(restaurant);
            _dbContext.SaveChanges();                      
        }

        public void Update(int id, UpdateRestaurantDto dto)
        {
            var restaurant = _dbContext
              .Restaurants          
              .FirstOrDefault(r => r.Id == id);

            if (restaurant is null)
                throw new NotFoundException("RestaurantNotFound");

            var authorizationResult = _authorizationService.AuthorizeAsync(_userContextService.User, restaurant,
                new ResourceOperationRequirement(ResourceOperation.Update)).Result;

            if(!authorizationResult.Succeeded)
            {
                throw new ForbidException();
            }

            restaurant.Name = dto.Name;
            restaurant.Description = dto.Description;
            restaurant.HasDelivery = dto.HasDelivery;
            
            _dbContext.SaveChanges();
        }
    }
}
