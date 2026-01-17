using Microsoft.AspNetCore.Mvc;
using MovieTicketBooking.Models;
using MySql.Data.MySqlClient;
using System.Data;
using System.Security.Cryptography;
using static Org.BouncyCastle.Math.EC.ECCurve;
using static SeatsController;

[ApiController]
[Route("api/seats")]
public class SeatsController : ControllerBase
{
    private readonly IConfiguration _config;

    public SeatsController(IConfiguration config)
    {
        _config = config;
    }
    public class Movie
    {
        public int Id { get; set; }
        public string MovieName { get; set; }
    }


    List<Movie> movies = new List<Movie>();




    //To get list of movies


    [HttpGet("movielist")]
    public IActionResult MoviesList()
    {
        using var conn = new MySqlConnection(_config.GetConnectionString("MySqlConnection"));
        conn.Open();
        string q = "SELECT show_id, movie_name FROM movie_shows; ";
        MySqlCommand cmd = new MySqlCommand(q, conn);
        MySqlDataReader dr = cmd.ExecuteReader();
        while (dr.Read())
        {
            movies.Add(new Movie
            {
                Id = Convert.ToInt32(dr["show_id"]),
                MovieName = dr["movie_name"].ToString()
            });
        }
        return Ok(movies);
    }




    //To get list of total seats of a movie

    [HttpGet("{id:int}/total")]
    public IActionResult NoOfSeats(int id)
    {
        using var conn = new MySqlConnection(_config.GetConnectionString("MySqlConnection"));
        conn.Open();
        string q = "SELECT movie_name , total_seats FROM movie_shows where show_id =@id; ";
        


        MySqlCommand cmd = new MySqlCommand(q, conn);
        cmd.Parameters.AddWithValue("@id", id);
        MySqlDataReader dr = cmd.ExecuteReader();
        if (dr.Read())
        {
            return Ok(new
            {
                MovieName = dr["movie_name"].ToString(),
                TotalSeats = Convert.ToInt32(dr["total_seats"])
            });
        }

        return NotFound("Show not found");

    }



    //To get list of available seats of a movie
    [HttpGet("{id:int}/available")]
    public IActionResult AvailableSeats(int id)
    {
        using var conn = new MySqlConnection(
            _config.GetConnectionString("MySqlConnection"));

        conn.Open();

        string query = "SELECT COUNT(*) FROM movie_shows WHERE show_id = @id;";
        MySqlCommand show = new MySqlCommand(query, conn);
        show.Parameters.AddWithValue("@id", id);
        int no = Convert.ToInt32(show.ExecuteScalar());
        if(no==0)
        {
            return NotFound("Show not found");
        }



        string q = "SELECT COUNT(*) From movie_seats where show_id = @id and status ='AVAILABLE' group by status; ";
        MySqlCommand cmd = new MySqlCommand(q, conn);
        cmd.Parameters.AddWithValue("@id", id);
        int availableSeats = Convert.ToInt32(cmd.ExecuteScalar());

        return Ok(new { availableSeats = availableSeats });

        
    }





    //To get list of held seats of a movie

    [HttpGet("{id:int}/held")]
    public IActionResult HeldSeats(int id)
    {
        using var conn = new MySqlConnection(
            _config.GetConnectionString("MySqlConnection"));

        conn.Open();

        string query = "SELECT COUNT(*) FROM movie_shows WHERE show_id = @id;";
        MySqlCommand show = new MySqlCommand(query, conn);
        show.Parameters.AddWithValue("@id", id);
        int no = Convert.ToInt32(show.ExecuteScalar());
        if (no == 0)
        {
            return NotFound("Show not found");
        }



        string q = "SELECT COUNT(*) From movie_seats where show_id = @id and status ='HELD' group by status; ";
        MySqlCommand cmd = new MySqlCommand(q, conn);
        cmd.Parameters.AddWithValue("@id", id);
        int heldSeats = Convert.ToInt32(cmd.ExecuteScalar());

        return Ok(new { HeldSeats = heldSeats });
    }





    //To get list of booked seats of a movie

    [HttpGet("{id:int}/booked")]
    public IActionResult BookedSeats(int id)
    {
        using var conn = new MySqlConnection(
            _config.GetConnectionString("MySqlConnection"));

        conn.Open();

        string query = "SELECT COUNT(*) FROM movie_shows WHERE show_id = @id;";
        MySqlCommand show = new MySqlCommand(query, conn);
        show.Parameters.AddWithValue("@id", id);
        int no = Convert.ToInt32(show.ExecuteScalar());
        if (no == 0)
        {
            return NotFound("Show not found");
        }





        string q = "SELECT COUNT(*) From movie_seats where show_id = @id and status ='BOOKED' group by status; ";
        MySqlCommand cmd = new MySqlCommand(q, conn);
        cmd.Parameters.AddWithValue("@id", id);
        int bookedSeats = Convert.ToInt32(cmd.ExecuteScalar());

        return Ok(new { BookeddSeats = bookedSeats });
    }






    //To hold a seat
    [HttpPost("hold")]
    public IActionResult HoldSeat([FromBody] HoldSeatRequest request)
    {
        using var conn = new MySqlConnection(
            _config.GetConnectionString("MySqlConnection"));

        conn.Open();

        using var tx = conn.BeginTransaction(IsolationLevel.Serializable);

        try
        {
           
            var cleanupCmd = new MySqlCommand(
                @"UPDATE movie_seats
              SET status = 'AVAILABLE',
                  hold_by = NULL,
                  hold_expires_at = NULL
              WHERE status = 'HELD'
              AND hold_expires_at <= UTC_TIMESTAMP()",
                conn, tx);

            cleanupCmd.ExecuteNonQuery();

            
            var checkCmd = new MySqlCommand(
                @"SELECT status
              FROM movie_seats
              WHERE seat_id = @seatId
              FOR UPDATE",
                conn, tx);

            checkCmd.Parameters.AddWithValue("@seatId", request.SeatId);

            var status = checkCmd.ExecuteScalar()?.ToString();

            if (status == null)
                return NotFound("Seat not found");

            if (status != "AVAILABLE")
                return BadRequest("Seat not available");

           
            var holdCmd = new MySqlCommand(
                @"UPDATE movie_seats
              SET status = 'HELD',
                  hold_by = @userId,
                  hold_expires_at = DATE_ADD(UTC_TIMESTAMP(), INTERVAL 2 MINUTE)
              WHERE seat_id = @seatId",
                conn, tx);

            holdCmd.Parameters.AddWithValue("@userId", request.UserId);
            holdCmd.Parameters.AddWithValue("@seatId", request.SeatId);

            holdCmd.ExecuteNonQuery();

            tx.Commit();
            return Ok("Seat is successfully in hold for 2 minutes");
        }
        catch
        {
            tx.Rollback();
            return StatusCode(500, "Error holding seat");
        }
    }




    //To book a seat

    [HttpPost("confirm")]
    public IActionResult ConfirmBooking([FromBody] ConfirmBookingRequest request)
    {
        using var conn = new MySqlConnection(
            _config.GetConnectionString("MySqlConnection"));

        conn.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            // 🔹 1. Cleanup expired holds
            var cleanupCmd = new MySqlCommand(
            @"UPDATE movie_seats
          SET status = 'AVAILABLE',
              hold_by = NULL,
              hold_expires_at = NULL
          WHERE status = 'HELD'
          AND hold_expires_at <= UTC_TIMESTAMP()",
            conn, tx);

            cleanupCmd.ExecuteNonQuery();

            // 🔹 2. Book seat (DIRECT or FROM HOLD)
            var bookCmd = new MySqlCommand(
            @"UPDATE movie_seats
          SET status = 'BOOKED',
              hold_by = NULL,
              hold_expires_at = NULL
          WHERE seat_id = @seatId
          AND (
                status = 'AVAILABLE'
                OR (
                    status = 'HELD'
                    AND hold_by = @userId
                    AND hold_expires_at > UTC_TIMESTAMP()
                )
              )",
            conn, tx);

            bookCmd.Parameters.AddWithValue("@seatId", request.SeatId);
            bookCmd.Parameters.AddWithValue("@userId", request.UserId);

            int rows = bookCmd.ExecuteNonQuery();

            if (rows == 0)
            {
                tx.Rollback();
                return BadRequest(
                    "Seat already booked, held by another user, or hold expired");
            }

            tx.Commit();
            return Ok("Seat booked successfully");
        }
        catch
        {
            tx.Rollback();
            return StatusCode(500, "Booking failed");
        }
    }














}