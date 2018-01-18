package pages.app

import org.openqa.selenium.By

import geb.Page

class PrivacyPage extends Page {

	static at = { title == "Privacy | BC Gov News" }
	static url = "/privacy"
}
