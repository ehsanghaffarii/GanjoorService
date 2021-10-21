﻿using Microsoft.EntityFrameworkCore;
using RMuseum.Models.Ganjoor;
using RSecurityBackend.Models.Generic;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using RSecurityBackend.Services.Implementation;
using RMuseum.Models.Ganjoor.ViewModels;
using System.Collections.Generic;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// IGanjoorService implementation
    /// </summary>
    public partial class GanjoorService : IGanjoorService
    {
        /// <summary>
        /// regenerate half centuries
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> RegenerateHalfCenturiesAsync()
        {
            try
            {
                var oldOnes = await _context.GanjoorHalfCenturies.ToArrayAsync();
                _context.RemoveRange(oldOnes);
                await _context.SaveChangesAsync();

                var periods = new List<GanjoorHalfCentury>
                {
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 1,
                        Name = "قرن سوم",
                        StartYear = 0,
                        EndYear = 300,
                        ShowInTimeLine = true,
                    },
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 2,
                        Name = "سوم و چهارم",
                        StartYear = 250,
                        EndYear = 350,
                        ShowInTimeLine = false,
                    },
                     new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 3,
                        Name = "قرن چهارم",
                        StartYear = 300,
                        EndYear = 400,
                        ShowInTimeLine = true,
                    },
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 4,
                        Name = "چهارم و پنجم",
                        StartYear = 350,
                        EndYear = 450,
                        ShowInTimeLine = false,
                    },
                     new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 5,
                        Name = "قرن پنجم",
                        StartYear = 400,
                        EndYear = 500,
                        ShowInTimeLine = true,
                    },
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 6,
                        Name = "پنجم و ششم",
                        StartYear = 450,
                        EndYear = 550,
                        ShowInTimeLine = false,
                    },
                     new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 7,
                        Name = "قرن ششم",
                        StartYear = 500,
                        EndYear = 600,
                        ShowInTimeLine = true,
                    },
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 8,
                        Name = "ششم و هفتم",
                        StartYear = 550,
                        EndYear = 650,
                        ShowInTimeLine = false,
                    },
                     new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 9,
                        Name = "قرن هفتم",
                        StartYear = 600,
                        EndYear = 700,
                        ShowInTimeLine = true,
                    },
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 10,
                        Name = "هفتم و هشتم",
                        StartYear = 650,
                        EndYear = 750,
                        ShowInTimeLine = false,
                    },
                     new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 11,
                        Name = "قرن هشتم",
                        StartYear = 700,
                        EndYear = 800,
                        ShowInTimeLine = true,
                    },
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 12,
                        Name = "هشتم و نهم",
                        StartYear = 750,
                        EndYear = 850,
                        ShowInTimeLine = false,
                    },
                     new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 13,
                        Name = "قرن نهم",
                        StartYear = 800,
                        EndYear = 900,
                        ShowInTimeLine = true,
                    },
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 14,
                        Name = "نهم و دهم",
                        StartYear = 850,
                        EndYear = 950,
                        ShowInTimeLine = false,
                    },
                     new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 15,
                        Name = "قرن دهم",
                        StartYear = 900,
                        EndYear = 1000,
                        ShowInTimeLine = true,
                    },
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 16,
                        Name = "دهم و یازدهم",
                        StartYear = 950,
                        EndYear = 1050,
                        ShowInTimeLine = false,
                    },
                     new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 17,
                        Name = "قرن یازدهم",
                        StartYear = 1000,
                        EndYear = 1100,
                        ShowInTimeLine = true,
                    },
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 18,
                        Name = "یازذهم و دوازدهم",
                        StartYear = 1050,
                        EndYear = 1150,
                        ShowInTimeLine = false,
                    },
                     new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 19,
                        Name = "قرن دوازدهم",
                        StartYear = 1100,
                        EndYear = 1200,
                        ShowInTimeLine = true,
                    },
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 20,
                        Name = "دوازدهم و سیزدهم",
                        StartYear = 1150,
                        EndYear = 1250,
                        ShowInTimeLine = false,
                    },
                     new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 21,
                        Name = "قرن سیزدهم",
                        StartYear = 1200,
                        EndYear = 1300,
                        ShowInTimeLine = true,
                    },
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 22,
                        Name = "سیزدهم و چهاردهم",
                        StartYear = 1250,
                        EndYear = 1350,
                        ShowInTimeLine = false,
                    },
                     new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 23,
                        Name = "قرن چهاردهم",
                        StartYear = 1300,
                        EndYear = 1400,
                        ShowInTimeLine = true,
                    },
                    new GanjoorHalfCentury()
                    {
                        HalfCenturyOrder = 24,
                        Name = "چهاردهم و پانزدهم",
                        StartYear = 1350,
                        EndYear = 1450,
                        ShowInTimeLine = false,
                    },
    
                };

                var poets = await _context.GanjoorPoets.AsNoTracking().Where(p => p.BirthYearInLHijri != 0).OrderBy(p => new { p.BirthYearInLHijri, p.DeathYearInLHijri }).ToArrayAsync();


                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }
    }
}
