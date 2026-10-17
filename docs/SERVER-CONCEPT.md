# Principles

Databases are stored on a dedicated web server. It doesn't know what is inside database files.
Basically the database only stores files so such server could be used for purposes other than Kofrenot.

* Server handles multiple users.
	* Each user has his set of databases.
* Several roles: admin & user
* No web site exposed. Client App interacts with server using a REST API.
* No admin UI for now. Admin unlocks accessing data files.

# REST API

* User can sign up:
	* email and password are given
	* email must be verified, email is sent to user inviting him to click on a validation link.
	* admin must unlock user account
* User can sign in:
	* Possible only when unlocked
* User can create a database:
	* name is given
* User can delete and clear a database. User validation required. Server sends a random phrase that user must key and sent to server thru another command.
* User can list his databases
* User can get statistics on a database: occupied disk space, number of files...
* User can interacts with content of a database:
	* list files
	* download/upload files
	* lock whole database during upload or packing?

# Techs

* .NET. Kestrel?
* Database: 
	* json files
	* sensitive data is encrypted. encryption key on server config file is ok.
	* models:
		* users
		* user databases
* Security:
	* JSON Web Token (JWT)
	* Token refreshed reguarly