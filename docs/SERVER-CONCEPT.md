# Principles

Vaults are stored on a dedicated web server. It doesn't know what is inside vault files.
Basically the vault only stores files so such server could be used for purposes other than Kofrenot.

* Server handles multiple users.
	* Each user has his set of vaults.
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
* User can create a vault:
	* the server knows it by its id only: its name stays encrypted in the vault ([ARCHITECTURE.md](ARCHITECTURE.md#registered-vaults))
	* to decide: how a device that never unlocked a vault shows its name: a placeholder (id, size) until unlocked once, or a copy of the name encrypted with an account key, stored by the server without reading it
* User can delete and clear a vault. User validation required. Server sends a random phrase that user must key and sent to server thru another command.
* User can list his vaults
* User can get statistics on a vault: occupied disk space, number of files...
* User can interacts with content of a vault:
	* list files
	* download/upload files
	* lock whole vault during upload or packing?

# Techs

* .NET. Kestrel?
* Database: 
	* json files
	* sensitive data is encrypted. encryption key on server config file is ok.
	* models:
		* users
		* user vaults
* Security:
	* JSON Web Token (JWT)
	* Token refreshed reguarly


# File transfer

* Vaults can content a lot of small files.
* To reduce transfer durations, several files can be transmitted at once.
* In the opposite, if a file is huge, it can be split to several chunk during transfer.

