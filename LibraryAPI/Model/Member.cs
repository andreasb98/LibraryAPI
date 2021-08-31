using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#nullable disable

namespace LibraryAPI.Model
{
    [Table("Member")]
    public partial class Member
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("name")]
        [StringLength(100)]
        public string Name { get; set; }
        [Column("mobile")]
        [StringLength(20)]
        public string Mobile { get; set; }
        [Column("email")]
        [StringLength(100)]
        public string Email { get; set; }
        [Column("password")]
        [StringLength(100)]
        public string Password { get; set; }
    }
}
