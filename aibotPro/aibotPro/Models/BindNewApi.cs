﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace aibotPro.Models
{
    [Table("BindNewApi")]
    public partial class BindNewApi
    {
        [Key]
        public int Id { get; set; }
        [StringLength(500)]
        public string Account { get; set; }
        public int? ApiId { get; set; }
        [StringLength(50)]
        public string ApiUserName { get; set; }
    }
}