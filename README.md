# MovieTicketBooking

Tech Stack
----------

Backend: ASP.NET Core Web API

Database Access: ADO.NET

Database: MySQL

Architecture: RESTful APIs with transactional consistency

Each funtionality is done using different Api calls.


Key Features
--------

List all movies

Get total seats for a movie

Get available seats for a movie

Get held seats for a movie

Get booked seats for a movie

Hold a seat temporarily

Book a seat (from hold or directly)



Seat Status Lifecycle
---

Each seat can be in one of three states:

AVAILABLE:	Seat is free and can be booked.

HELD:	Seat is temporarily reserved.

BOOKED	:Seat is permanently booked.


Seat Hold Logic
------

A seat can be held for 2 minutes

If the seat is not booked within 2 minutes, it automatically becomes AVAILABLE

Expired holds are cleaned up during booking/holding operations

 Booking Rules
 ---

A HELD seat can only be booked by the same user

If another user tries to book it →  Error

A seat can be booked directly without holding

All booking operations are done inside database transactions


