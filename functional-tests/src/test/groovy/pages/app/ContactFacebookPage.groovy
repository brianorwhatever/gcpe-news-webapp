package pages.app

import org.openqa.selenium.By

import geb.Page

class ContactFacebookPage extends Page {

	//static at = { $("a", name:"facebook").displayed == true }
	static at = { driver.findElement(By.xpath("//*[@id='main-content']/div[1]/div[2]/div[1]/h4")).displayed == true }
	static url = "/connect#facebook"
}
